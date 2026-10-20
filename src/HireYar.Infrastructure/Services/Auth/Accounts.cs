using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HireYar.Infrastructure.Auth;

public sealed class Accounts(AppDbContext db, UserManager<ApplicationUser> users, IPhoneOtpSender sms, IOptions<SecurityOptions> options, IOptions<KavenegarOptions> otpOptions, TimeProvider time, ILogger<Accounts> logger, HireYar.Application.Subscriptions.SubscriptionService subscriptions) : IAccounts
{
    private SecurityOptions Settings => options.Value;
    private DateTimeOffset Now => time.GetUtcNow();
    private string OtpHash(string mobile, string code) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Settings.SigningKey), Encoding.UTF8.GetBytes($"{mobile}:{code}")));
    public async Task<OtpRequestResult> RequestOtpAsync(RequestOtpRequest r, CancellationToken ct)
    {
        var now = Now;
        var fixedMode = otpOptions.Value.UseFixedOtp;
        var code = fixedMode ? otpOptions.Value.FixedOtpCode : RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        OtpChallenge challenge;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            challenge = await db.OtpChallenges.FromSqlInterpolated($"SELECT * FROM \"OtpChallenges\" WHERE \"Mobile\" = {r.Mobile} FOR UPDATE").SingleOrDefaultAsync(ct)
                ?? new OtpChallenge { Mobile = r.Mobile };
            if (challenge.CreatedAtUtc > now.AddSeconds(-60)) throw new AppException(429, "rate_limited", "Please wait before requesting another code.");
            if (db.Entry(challenge).State == EntityState.Detached) db.OtpChallenges.Add(challenge);
            challenge.CodeHash = OtpHash(r.Mobile, code);
            challenge.CreatedAtUtc = now; challenge.ExpiresAtUtc = now.AddMinutes(5);
            challenge.ConsumedAtUtc = null; challenge.Delivered = fixedMode; challenge.FailedAttempts = 0;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        if (fixedMode) return new(challenge.ExpiresAtUtc);
        try
        {
            await sms.SendAsync(r.Mobile, code, ct);
            challenge.Delivered = true;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("OTP delivery failed. EventCode={EventCode}", "otp_delivery_failed");
            throw new AppException(503, "otp_delivery_failed", "Could not send the verification code.");
        }
        return new(challenge.ExpiresAtUtc);
    }
    public Task<SessionResult> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName) ||
            r.FirstName.Trim().Length + r.LastName.Trim().Length + 1 > 200)
            throw AppException.Invalid("invalid_registration_name");
        return VerifyAsync(new(r.Mobile, r.Code), null, r, ct);
    }
    public Task<SessionResult> VerifyOtpAsync(VerifyOtpRequest r, CancellationToken ct) => VerifyAsync(r, null, null, ct);
    public Task<SessionResult> AcceptTeamInvitationAsync(Guid id, VerifyOtpRequest r, CancellationToken ct) => VerifyAsync(r, id, null, ct);
    private async Task<SessionResult> VerifyAsync(VerifyOtpRequest r, Guid? invitationId, RegisterRequest? registration, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TeamMembershipLocks.LockMobileAsync(db, r.Mobile, ct);
        var challenge = await db.OtpChallenges.FromSqlInterpolated($"SELECT * FROM \"OtpChallenges\" WHERE \"Mobile\" = {r.Mobile} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (challenge is null || !challenge.Delivered || challenge.ConsumedAtUtc.HasValue || challenge.ExpiresAtUtc <= Now || challenge.FailedAttempts >= 5)
            throw AppException.Unauthorized();
        var actual = Convert.FromHexString(challenge.CodeHash);
        var supplied = Convert.FromHexString(OtpHash(r.Mobile, r.Code));
        if (!CryptographicOperations.FixedTimeEquals(actual, supplied))
        {
            challenge.FailedAttempts++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw AppException.Unauthorized();
        }
        var u = await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == r.Mobile, ct);
        if (invitationId is not null)
        {
            var invitation = await db.MobileTeamInvitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.Mobile == r.Mobile, ct)
                ?? throw AppException.NotFound();
            if (invitation.AcceptedAtUtc is not null || invitation.CancelledAtUtc is not null || invitation.ExpiresAtUtc <= Now)
                throw new AppException(409, "invitation_unavailable", "Invitation is no longer available.");
            var org = await db.Organizations.FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {invitation.OrganizationId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct)
                ?? throw AppException.NotFound();
            if (invitation.ExpiresAtUtc <= Now)
                throw new AppException(409, "invitation_unavailable", "Invitation is no longer available.");
            if (u is not null && (u.OrganizationId != org.Id || u.Role != OrganizationRole.Recruiter || u.IsActive))
                throw new AppException(409, "mobile_unavailable", "This mobile cannot accept the invitation.");
            await subscriptions.RequireTeamCapacityLockedAsync(org.Id, true, ct);
            var invitedName = !string.IsNullOrWhiteSpace(invitation.FirstName) && !string.IsNullOrWhiteSpace(invitation.LastName)
                ? $"{invitation.FirstName.Trim()} {invitation.LastName.Trim()}" : null;
            if (u is null)
            {
                u = new ApplicationUser { Id = Guid.NewGuid(), UserName = r.Mobile, PhoneNumber = r.Mobile,
                    PhoneNumberConfirmed = true, FullName = invitedName ?? "کاربر", OrganizationId = org.Id, Role = OrganizationRole.Recruiter,
                    CreatedAtUtc = Now, UpdatedAtUtc = Now };
                Check(await users.CreateAsync(u));
            }
            else
            {
                if (await users.IsLockedOutAsync(u)) throw AppException.Unauthorized();
                if (invitedName is not null) u.FullName = invitedName;
                u.IsActive = true; u.PhoneNumberConfirmed = true; u.UpdatedAtUtc = Now;
                Check(await users.UpdateSecurityStampAsync(u));
            }
            if (invitation.ExpiresAtUtc <= Now)
                throw new AppException(409, "invitation_unavailable", "Invitation is no longer available.");
            invitation.AcceptedAtUtc = Now;
        }
        else if (registration is not null || u is null)
        {
            if (u is not null)
                throw new AppException(409, "mobile_already_registered", "This mobile is already registered. Sign in instead.");
            if (await db.MobileTeamInvitations.AnyAsync(x => x.Mobile == r.Mobile && x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > Now, ct))
                throw new AppException(409, "team_invitation_pending", "Accept the team invitation before creating an organization.");
            if (registration is null)
                throw new AppException(409, "registration_required", "Complete registration before signing in.");
            var email = registration.Email.Trim();
            var normalizedEmail = users.NormalizeEmail(email);
            if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct))
                throw new AppException(409, "email_unavailable", "This email is unavailable.");
            var org = new Organization { Name = "سازمان من", CreatedAtUtc = Now, UpdatedAtUtc = Now };
            db.Organizations.Add(org); await db.SaveChangesAsync(ct);
            u = new ApplicationUser { Id = Guid.NewGuid(), UserName = r.Mobile, PhoneNumber = r.Mobile,
                PhoneNumberConfirmed = true, Email = email, EmailConfirmed = false,
                FullName = $"{registration.FirstName.Trim()} {registration.LastName.Trim()}",
                ReferralCode = string.IsNullOrWhiteSpace(registration.ReferralCode) ? null : registration.ReferralCode.Trim(),
                OrganizationId = org.Id, Role = OrganizationRole.Owner,
                CreatedAtUtc = Now, UpdatedAtUtc = Now };
            Check(await users.CreateAsync(u));
            await db.WithSalesLockAsync(async () => {
                var lead = await db.SalesLeads.SingleOrDefaultAsync(x => x.Mobile == r.Mobile, ct);
                if (lead is not null && lead.OrganizationId is null) {
                    lead.OrganizationId = org.Id; lead.RegisteredAtUtc = u.CreatedAtUtc;
                    db.SalesAudits.Add(new() { ActorId = u.Id, LeadId = lead.Id, Action = "Registered", CreatedAtUtc = Now });
                }
                return true;
            }, ct);
            var plan = await db.SubscriptionPlans.AsNoTracking().SingleAsync(ct);
            var trialStart = Now;
            db.SignupTrials.Add(new() { OrganizationId = org.Id, StartedAtUtc = trialStart,
                ExpiresAtUtc = trialStart.AddDays(2), TeamUsers = plan.IncludedUsers, RetentionDays = plan.RetentionDays });
        }
        if (!u.IsActive || u.Role is not (OrganizationRole.Owner or OrganizationRole.Recruiter) || await users.IsLockedOutAsync(u)) throw AppException.Unauthorized();
        u.PhoneNumberConfirmed = true;
        challenge.ConsumedAtUtc = Now;
        var session = Issue(u, Guid.NewGuid());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return session;
    }
    public async Task<SessionResult> RefreshAsync(string token, CancellationToken ct)
    {
        if (token.Length != 64) throw AppException.Unauthorized();
        var hash = TokenSecrets.Hash(token);
        var owner = await db.RefreshTokens.AsNoTracking().Where(x => x.Hash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (owner is null) throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Every session mutation locks the user first; concurrent refreshes re-read the token only after this lock.
        var u = await LockUser(owner.Value, ct) ?? throw AppException.Unauthorized();
        var old = await db.RefreshTokens.SingleAsync(x => x.Hash == hash, ct);
        if (old.RevokedAtUtc.HasValue)
        {
            await Revoke(u.Id, old.FamilyId, ct); await tx.CommitAsync(ct); throw AppException.Unauthorized();
        }
        if (old.ExpiresAtUtc <= Now || !u.IsActive || u.Role is not (OrganizationRole.Owner or OrganizationRole.Recruiter) || !u.PhoneNumberConfirmed) throw AppException.Unauthorized();
        var result = Issue(u, old.FamilyId);
        var next = db.ChangeTracker.Entries<RefreshToken>().Single(x => x.State == EntityState.Added).Entity;
        old.RevokedAtUtc = Now; old.ReplacedById = next.Id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public async Task LogoutAsync(string token, CancellationToken ct)
    {
        if (token.Length != 64) return;
        var hash = TokenSecrets.Hash(token);
        var row = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.Hash == hash, ct); if (row is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockUser(row.UserId, ct); await Revoke(row.UserId, row.FamilyId, ct); await tx.CommitAsync(ct);
    }
    public async Task LogoutAllAsync(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockUser(userId, ct); await Revoke(userId, null, ct); await tx.CommitAsync(ct);
    }
    public async Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken ct) => await
        (from u in db.Users.AsNoTracking()
         join o in db.Organizations.AsNoTracking() on u.OrganizationId equals o.Id
         where u.Id == userId
         select new CurrentUserDto(u.Id, u.Email, u.PhoneNumber, u.FullName, u.OrganizationId, o.Name, u.Role)).SingleOrDefaultAsync(ct) ?? throw AppException.Unauthorized();
    public async Task<CurrentUserDto> UpdateAsync(Guid userId, NameRequest r, CancellationToken ct)
    {
        if (r.OrganizationName is not null && string.IsNullOrWhiteSpace(r.OrganizationName))
            throw AppException.Invalid("invalid_organization_name");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await LockUser(userId, ct) ?? throw AppException.Unauthorized();
        if (r.OrganizationName is not null)
        {
            if (u.Role != OrganizationRole.Owner) throw new AppException(403, "forbidden", "Owner access required.");
            var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == u.OrganizationId, ct)
                ?? throw new AppException(404, "not_found", "Organization not found.");
            org.Name = r.OrganizationName.Trim();
            org.UpdatedAtUtc = Now;
        }
        u.FullName = r.FullName.Trim(); u.UpdatedAtUtc = Now;
        Check(await users.UpdateAsync(u));
        await tx.CommitAsync(ct);
        return await GetAsync(userId, ct);
    }
    private Task<ApplicationUser?> LockUser(Guid id, CancellationToken ct) => db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private Task<int> Revoke(Guid userId, Guid? family, CancellationToken ct) => db.RefreshTokens.Where(x => x.UserId == userId && (!family.HasValue || x.FamilyId == family) && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
    private SessionResult Issue(ApplicationUser u, Guid family)
    {
        var raw = TokenSecrets.Create(); var expires = Now.AddMinutes(Settings.AccessMinutes); var refreshExpires = Now.AddDays(Settings.RefreshDays);
        db.RefreshTokens.Add(new RefreshToken { UserId = u.Id, FamilyId = family, Hash = TokenSecrets.Hash(raw), CreatedAtUtc = Now, ExpiresAtUtc = refreshExpires });
        var claims = new[] { new Claim("sub", u.Id.ToString()), new Claim("org", u.OrganizationId.ToString()), new Claim("role", u.Role.ToString()), new Claim("jti", Guid.NewGuid().ToString()), new Claim("account_stamp", u.SecurityStamp ?? "") };
        var jwt = new JwtSecurityToken(Settings.Issuer, Settings.Audience, claims, Now.UtcDateTime, expires.UtcDateTime, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, raw, refreshExpires);
    }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw AppException.Invalid("account_rejected"); }
}
