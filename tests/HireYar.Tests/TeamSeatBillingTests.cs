using System.Net;
using System.Net.Http.Json;
using HireYar.Application.Auth;
using HireYar.Application.Billing;
using HireYar.Application.Common;
using HireYar.Application.Organizations;
using HireYar.Domain.Billing;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace HireYar.Tests;

[Collection("Postgres")]
public sealed class TeamSeatBillingTests : IAsyncLifetime
{
    private readonly ApiFixture f = new();
    public Task InitializeAsync() => f.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)f).DisposeAsync();
    private static string Mobile() => "09" + Random.Shared.NextInt64(100_000_000, 1_000_000_000).ToString("D9");
    private async Task<PackageDto> Package(string code) => (await (await f.Browser().Send(HttpMethod.Get, "/api/pricing/packages")).Content.ReadFromJsonAsync<PricingResponse>(BillingJson.Options))!.Packages.Single(x => x.Code == code);
    private async Task<T> Read<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"HTTP {(int)response.StatusCode}: " + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(BillingJson.Options))!;
    }
    private async Task<SubscriptionDto> Buy(Account owner, string code = "basic", int? capacity = null)
    {
        var p = await Package(code);
        var pending = await Read<PurchaseResult>(await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/purchases", new SubscriptionPurchaseRequest(p.Periods.Single(x => x.Months == 1).Id, capacity)));
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Payment.Id}/verify")).StatusCode);
        return (await Read<SubscriptionOverviewDto>(await owner.Client.Send(HttpMethod.Get, "/api/billing/subscription"))).Current!;
    }
    private async Task<TeamSeatStateDto> State(Account owner) => await Read<TeamSeatStateDto>(await owner.Client.Send(HttpMethod.Get, "/api/billing/team-seats"));
    private async Task<TeamSeatQuoteDto> Quote(Account owner, int capacity, DateTimeOffset? at = null) =>
        await Read<TeamSeatQuoteDto>(await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/quote", new TeamSeatQuoteRequest(capacity, at)));
    private static TeamSeatQuoteRequest Request(TeamSeatQuoteDto quote) => new(quote.TargetCapacity, quote.QuotedAtUtc, quote.QuoteFingerprint);
    private async Task<TeamSeatCheckoutDto> Checkout(Account owner, TeamSeatQuoteDto quote) =>
        await Read<TeamSeatCheckoutDto>(await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(quote)));
    private async Task Invite(Account owner) => Assert.Equal(HttpStatusCode.Created,
        (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی"))).StatusCode);
    private async Task AddMembers(Account owner, int count)
    {
        await f.Db(async db => {
            var org = await db.Users.Where(x => x.Id == owner.Id).Select(x => x.OrganizationId).SingleAsync();
            for (var i = 0; i < count; i++) db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(),
                PhoneNumber = Mobile(), PhoneNumberConfirmed = true, OrganizationId = org, Role = OrganizationRole.Recruiter,
                FullName = "Synthetic member", CreatedAtUtc = f.Clock.GetUtcNow(), UpdatedAtUtc = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
        });
    }

    [PostgresFact]
    public async Task Included_users_and_pending_invitations_are_reserved_and_concurrent_last_free_seat_has_one_winner()
    {
        Assert.Equal(3, (await Package("basic")).IncludedTeamUsers);
        Assert.Equal(5, (await Package("advanced")).IncludedTeamUsers);
        var owner = await f.RegisterOwner(); await Buy(owner); await Invite(owner);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی"))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        var rejected = Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("team_capacity_payment_required", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(3, (await State(owner)).OccupiedUsers);
        var invitations = await Read<Page<TeamInvitationDto>>(await owner.Client.Send(HttpMethod.Get, "/api/team/invitations"));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.Send(HttpMethod.Delete, $"/api/team/invitations/{invitations.Items[0].Id}")).StatusCode);
        await Invite(owner);
        var advanced = await f.RegisterOwner(); await Buy(advanced, "advanced");
        for (var i = 0; i < 4; i++) await Invite(advanced);
        Assert.Equal(HttpStatusCode.Conflict, (await advanced.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی"))).StatusCode);
    }

    [PostgresFact]
    public async Task Fifteen_days_cost_half_and_only_verified_payment_grants_capacity_with_idempotent_invoice_history()
    {
        var owner = await f.RegisterOwner(); var other = await f.RegisterOwner(); var sub = await Buy(owner);
        var at = sub.ExpiresAtUtc.AddDays(-15);
        f.Clock.Offset = at - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        var quote = await Quote(owner, 4, at);
        Assert.Equal(100_000m, quote.AmountIrt); Assert.Equal(0.5m, quote.RemainingBillingMonths);
        var pending = await Checkout(owner, quote);
        Assert.Equal(PaymentType.TeamSeats, pending.Checkout.Payment.Type);
        Assert.Equal(3, (await State(owner)).Capacity);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Checkout.Payment.Id}/verify")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Checkout.Payment.Id}/verify")).StatusCode);
        Assert.Equal(4, (await State(owner)).Capacity);
        var invoice = await Read<InvoiceDto>(await owner.Client.Send(HttpMethod.Get, $"/api/billing/payments/{pending.Checkout.Payment.Id}/invoice"));
        Assert.Equal(100_000m, invoice.TotalAmount); Assert.Equal(100_000m, invoice.TeamSeats!.AmountIrt);
        var history = await Read<FinancialHistoryDto>(await owner.Client.Send(HttpMethod.Get, "/api/billing/financial-history?creditType=teamSeats"));
        Assert.Equal(100_000m, history.Totals.TeamSeatsPaidIrt); Assert.Equal("IRT", Assert.Single(history.Transactions.Items).Currency);
        await f.Db(async db => { Assert.Single(await db.TeamSeatPurchases.ToListAsync()); Assert.Empty(await db.AiCreditTransactions.ToListAsync()); });
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Checkout.Payment.Id}/verify")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Send(HttpMethod.Get, $"/api/billing/payments/{pending.Checkout.Payment.Id}/invoice")).StatusCode);
    }

    [PostgresFact]
    public async Task Capacity_thirty_opens_user_thirty_one_and_higher_for_free_and_renewal_caps_billable_users()
    {
        var owner = await f.RegisterOwner(); await Buy(owner, capacity: 30);
        await AddMembers(owner, 29);
        var state = await State(owner); Assert.True(state.IsUnlimited); Assert.Equal(30, state.OccupiedUsers);
        await Invite(owner); await Invite(owner);
        Assert.Equal(32, (await State(owner)).OccupiedUsers);
        var period = (await Package("basic")).Periods.Single(x => x.Months == 1);
        var summary = await Read<CheckoutSummaryDto>(await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(period.Id, "renewal")));
        Assert.Equal(30, summary.TeamCapacity); Assert.Equal(27 * 200_000m, summary.TeamSeatAmountIrt);
        Assert.Equal(period.Price + summary.TeamSeatAmountIrt, summary.SubtotalAmount);
        var result = await Read<PanelCheckoutResult>(await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/checkout",
            new SubscriptionQuoteRequest(period.Id, "renewal", summary.QuoteFingerprint)));
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{result.Checkout.Payment.Id}/verify")).StatusCode);
        await f.Db(async db => {
            var purchase = await db.SubscriptionPurchases.SingleAsync(x => x.PaymentId == result.Checkout.Payment.Id);
            Assert.Equal(30, purchase.TeamCapacitySnapshot); Assert.Equal(5_400_000m, purchase.TeamSeatAmountIrtSnapshot);
        });
    }

    [PostgresFact]
    public async Task Admin_can_change_all_team_rules_and_existing_contracts_and_pending_checkouts_keep_snapshots()
    {
        var admin = await f.RegisterAdmin(); var p = await Package("advanced");
        var owner = await f.RegisterOwner(); await Buy(owner, "advanced");
        var quote = await Quote(owner, 6); var pending = await Checkout(owner, quote);
        var changed = await Read<PackageDto>(await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}",
            new PackageUpdateRequest(new(p.Code, p.Name, p.Description, p.IsActive, p.IsPublic, p.SortOrder,
                p.AiPricePerMinuteUsd, p.RetentionDays, 7, 9, 350_000m), p.RowVersion)));
        Assert.Equal(7, changed.IncludedTeamUsers); Assert.Equal(9, changed.PaidTeamUserLimit); Assert.Equal(350_000m, changed.TeamUserMonthlyPriceIrt);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}",
            new PackageUpdateRequest(new(p.Code, p.Name, p.Description, true, true, 0, p.AiPricePerMinuteUsd, p.RetentionDays), p.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Checkout.Payment.Id}/verify")).StatusCode);
        var oldState = await State(owner); Assert.Equal(5, oldState.IncludedTeamUsers); Assert.Equal(200_000m, oldState.TeamUserMonthlyPriceIrt); Assert.Equal(6, oldState.Capacity);
        var fresh = await f.RegisterOwner(); await Buy(fresh, "advanced", 9);
        var state = await State(fresh); Assert.True(state.IsUnlimited); Assert.Equal(7, state.IncludedTeamUsers);
        await AddMembers(fresh, 8); await Invite(fresh);
        var invalid = new PackageUpdateRequest(new(p.Code, p.Name, p.Description, true, true, 0, p.AiPricePerMinuteUsd, p.RetentionDays, 10, 9, 1.5m), changed.RowVersion);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}", invalid)).StatusCode);
    }

    [PostgresFact]
    public async Task Stale_quote_duplicate_checkout_failed_or_mismatched_verification_never_grant_capacity()
    {
        var owner = await f.RegisterOwner(); await Buy(owner);
        var quote = await Quote(owner, 4);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(quote) with { QuoteFingerprint = new string('0', 64) })).StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(quote))));
        var pending = await Read<TeamSeatCheckoutDto>(Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        f.PaymentGateway.Results[pending.Checkout.Payment.Id] = new(true, "synthetic-mismatch", quote.AmountIrt + 1, "IRT");
        var failed = await Read<PaymentDto>(await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{pending.Checkout.Payment.Id}/verify"));
        Assert.Equal(PaymentStatus.Failed, failed.Status); Assert.Equal(3, (await State(owner)).Capacity);
        var replacement = await Checkout(owner, await Quote(owner, 4));
        await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{replacement.Checkout.Payment.Id}/verify");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(quote))).StatusCode);
        var expired = await Quote(owner, 5);
        f.Clock.Offset = TimeSpan.FromMinutes(6);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(expired))).StatusCode);
        Assert.Equal(4, (await State(owner)).Capacity);
    }

    [PostgresFact]
    public async Task Team_payment_history_and_database_contracts_are_immutable_scoped_and_downgrade_is_blocked()
    {
        var owner = await f.RegisterOwner(); await Buy(owner);
        var checkout = await Checkout(owner, await Quote(owner, 4));
        await owner.Client.Send(HttpMethod.Post, $"/api/billing/payments/{checkout.Checkout.Payment.Id}/verify");
        await f.Db(async db => {
            Assert.False(db.Database.HasPendingModelChanges());
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"TeamSeatPurchases\" SET \"TargetCapacity\" = 29"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            var seat = await db.TeamSeatPurchases.SingleAsync(); seat.AmountIrt++;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            var otherOrg = Guid.NewGuid();
            var cross = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TeamSeatPurchases\" (\"Id\", \"OrganizationId\", \"SubscriptionId\", \"PaymentId\", \"PreviousCapacity\", \"TargetCapacity\", \"MonthlyPriceIrt\", \"AmountIrt\", \"CreatedAtUtc\", \"QuotedAtUtc\", \"ExpiresAtUtc\") SELECT {Guid.NewGuid()}, {otherOrg}, \"SubscriptionId\", {Guid.NewGuid()}, 3, 4, \"MonthlyPriceIrt\", \"AmountIrt\", \"CreatedAtUtc\", \"QuotedAtUtc\", \"ExpiresAtUtc\" FROM \"TeamSeatPurchases\" LIMIT 1"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, cross.SqlState);
            var down = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261014052638_AddMobileTeamInvitationNames"));
            Assert.Equal(PostgresErrorCodes.RaiseException, down.SqlState);
        });
    }

    [PostgresFact]
    public async Task Acceptance_rechecks_subscription_capacity_after_change_and_does_not_consume_otp_on_failure()
    {
        var owner = await f.RegisterOwner(); var first = await Buy(owner, "advanced");
        await AddMembers(owner, 3);
        var invite = await Read<TeamInvitationDto>(await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی")));
        // Scheduled contracts are independent. Advance past this contract so fallback capacity is smaller.
        f.Clock.Offset = first.ExpiresAtUtc - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        var browser = f.Browser();
        await browser.Send(HttpMethod.Post, "/api/auth/otp/request", new RequestOtpRequest(invite.Mobile));
        // Keep the invitation live to isolate the capacity recheck rather than invitation expiry.
        await f.Db(async db => { var i = await db.MobileTeamInvitations.SingleAsync(x => x.Id == invite.Id); i.ExpiresAtUtc = f.Clock.GetUtcNow().AddHours(1); await db.SaveChangesAsync(); });
        var result = await browser.Send(HttpMethod.Post, $"/api/auth/team-invitations/{invite.Id}/accept", new VerifyOtpRequest(invite.Mobile, f.PhoneOtp.Codes[invite.Mobile]));
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode); Assert.Contains("team_capacity_payment_required", await result.Content.ReadAsStringAsync());
        await f.Db(async db => { Assert.Null((await db.MobileTeamInvitations.SingleAsync(x => x.Id == invite.Id)).AcceptedAtUtc); Assert.Null((await db.OtpChallenges.SingleAsync(x => x.Mobile == invite.Mobile)).ConsumedAtUtc); });
    }

    [PostgresFact]
    public async Task Trial_uses_catalog_snapshot_zero_price_is_unlimited_and_disabled_gateway_creates_no_checkout()
    {
        var trial = await f.RegisterOwner(signupTrial: true);
        Assert.Equal(5, (await State(trial)).Capacity);
        var admin = await f.RegisterAdmin(); var p = await Package("advanced");
        await Read<PackageDto>(await admin.Client.Send(HttpMethod.Put, $"/api/admin/pricing/packages/{p.Id}",
            new PackageUpdateRequest(new(p.Code, p.Name, p.Description, true, true, 0, p.AiPricePerMinuteUsd, p.RetentionDays, 7, 12, 0), p.RowVersion)));
        Assert.Equal(5, (await State(trial)).Capacity);
        var newer = await f.RegisterOwner(signupTrial: true); var state = await State(newer);
        Assert.Equal(7, state.IncludedTeamUsers); Assert.True(state.IsUnlimited);
        for (var i = 0; i < 8; i++) await Invite(newer);
        var disabled = new ApiFixture { DisabledPayments = true }; await disabled.InitializeAsync();
        try
        {
            var owner = await disabled.RegisterOwner(signupTrial: true);
            var q = await Read<TeamSeatQuoteDto>(await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/quote", new TeamSeatQuoteRequest(6)));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/checkout", Request(q))).StatusCode);
            await disabled.Db(async db => { Assert.Empty(await db.TeamSeatPurchases.ToListAsync()); Assert.Empty(await db.Payments.ToListAsync()); });
        }
        finally { await ((IAsyncLifetime)disabled).DisposeAsync(); }
    }

    [PostgresFact]
    public async Task Paid_capacity_expires_with_subscription_and_renewal_prices_occupied_members_again()
    {
        var owner = await f.RegisterOwner(); var sub = await Buy(owner, capacity: 4);
        await AddMembers(owner, 3);
        f.Clock.Offset = sub.ExpiresAtUtc - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        var expired = await State(owner); Assert.Null(expired.SubscriptionId); Assert.Equal(3, expired.Capacity);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.Send(HttpMethod.Post, "/api/billing/team-seats/quote", new TeamSeatQuoteRequest(5))).StatusCode);
        var period = (await Package("basic")).Periods.Single(x => x.Months == 3);
        var q = await Read<CheckoutSummaryDto>(await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(period.Id, "renewal")));
        Assert.Equal(4, q.TeamCapacity); Assert.Equal(600_000m, q.TeamSeatAmountIrt);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.Send(HttpMethod.Post, "/api/billing/subscription/quote", new SubscriptionQuoteRequest(period.Id, "renewal", TeamCapacity: 3))).StatusCode);
    }

    [PostgresFact]
    public async Task Migration_preserves_legacy_team_capacity_and_cash_without_retroactive_charge()
    {
        var owner = await f.RegisterOwner(); var sub = await Buy(owner, "advanced");
        await f.Db(async db => {
            var before = await db.SubscriptionPurchases.SingleAsync();
            var amount = (await db.Payments.SingleAsync()).Amount;
            await db.GetService<IMigrator>().MigrateAsync("20261014052638_AddMobileTeamInvitationNames");
            var org = await db.Users.Where(x => x.Id == owner.Id).Select(x => x.OrganizationId).SingleAsync();
            // Populate the historical schema explicitly: the current EF user model may
            // contain columns added after the migration under test (e.g. ReferralCode).
            for (var i = 0; i < 6; i++)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AspNetUsers" ("Id", "UserName", "PhoneNumber", "PhoneNumberConfirmed", "OrganizationId", "Role", "FullName",
                        "EmailConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "IsActive", "IsSystemAdmin", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES ({Guid.NewGuid()}, {Guid.NewGuid().ToString()}, {Mobile()}, true, {org}, 'Recruiter', 'Legacy synthetic member',
                        false, false, false, 0, true, false, {f.Clock.GetUtcNow()}, {f.Clock.GetUtcNow()})
                    """);
            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync();
            var after = await db.SubscriptionPurchases.SingleAsync();
            Assert.Equal(before.Id, after.Id); Assert.Equal(before.PriceAmountSnapshot, after.PriceAmountSnapshot);
            Assert.Equal(amount, (await db.Payments.SingleAsync()).Amount);
            Assert.Equal(5, after.IncludedTeamUsersSnapshot); Assert.Equal(7, after.TeamCapacitySnapshot); Assert.Equal(0m, after.TeamSeatAmountIrtSnapshot);
            Assert.False(db.Database.HasPendingModelChanges());
        });
        var state = await State(owner); Assert.Equal(sub.Id, state.SubscriptionId); Assert.Equal(7, state.Capacity);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.Send(HttpMethod.Post, "/api/team/invitations", new InviteTeamMemberRequest(Mobile(), "نام", "آزمایشی"))).StatusCode);
    }
}

public sealed class TeamSeatMathTests
{
    [Fact]
    public void Monthly_proration_and_thirty_user_boundary_match_the_business_rule()
    {
        var now = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(100_000m, TeamSeatMath.ProratedAmount(1, 200_000m, now, now.AddDays(15)));
        Assert.Equal(200_000m, TeamSeatMath.ProratedAmount(1, 200_000m, now, now.AddDays(30)));
        Assert.Equal(600_000m, TeamSeatMath.ProratedAmount(1, 200_000m, now, now.AddDays(90)));
        Assert.Equal(27, TeamSeatMath.BillableUsers(30, 3, 30));
        Assert.Equal(27, TeamSeatMath.BillableUsers(31, 3, 30));
        Assert.Equal(25, TeamSeatMath.BillableUsers(1000, 5, 30));
    }
}
