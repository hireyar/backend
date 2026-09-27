using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HireYar.Infrastructure.Auth;

public sealed class Accounts(AppDbContext db, UserManager<ApplicationUser> users, IEmailSender email, IPhoneOtpSender sms, IOptions<SecurityOptions> options, IOptions<KavenegarOptions> otpOptions, TimeProvider time, ILogger<Accounts> logger) : IAccounts
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
    public async Task<SessionResult> VerifyOtpAsync(VerifyOtpRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
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
        if (u is null)
        {
            var org = new Organization { Name = "سازمان من", CreatedAtUtc = Now, UpdatedAtUtc = Now };
            db.Organizations.Add(org); await db.SaveChangesAsync(ct);
            u = new ApplicationUser { Id = Guid.NewGuid(), UserName = r.Mobile, PhoneNumber = r.Mobile,
                PhoneNumberConfirmed = true, FullName = "کاربر", OrganizationId = org.Id, Role = OrganizationRole.Owner,
                CreatedAtUtc = Now, UpdatedAtUtc = Now };
            Check(await users.CreateAsync(u));
        }
        if (!u.IsActive || await users.IsLockedOutAsync(u)) throw AppException.Unauthorized();
        u.PhoneNumberConfirmed = true;
        challenge.ConsumedAtUtc = Now;
        var session = Issue(u, Guid.NewGuid());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return session;
    }
    public async Task<RegistrationResult> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
            var normalized = users.NormalizeEmail(r.Email.Trim());
            if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw AppException.Conflict();
            var org = new Organization { Name = r.OrganizationName.Trim(), CreatedAtUtc = Now, UpdatedAtUtc = Now };
            var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = r.FullName.Trim(), Email = r.Email.Trim(), UserName = r.Email.Trim(), EmailConfirmed = Settings.AutoConfirmEmailOnRegistration, OrganizationId = org.Id, Role = OrganizationRole.Owner, CreatedAtUtc = Now, UpdatedAtUtc = Now };
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                db.Organizations.Add(org); await db.SaveChangesAsync(ct);
                Check(await users.CreateAsync(user, r.Password)); await tx.CommitAsync(ct);
            }
            return new(user.Id, !Settings.AutoConfirmEmailOnRegistration && await SendConfirmation(user, ct));
    }
    public async Task ConfirmAsync(TokenRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await LockUser(r.UserId, ct);
        if (u is null || u.EmailConfirmed) throw AppException.Invalid("invalid_token");
        CheckToken(await users.ConfirmEmailAsync(u, Decode(r.Token))); await tx.CommitAsync(ct);
    }
    public async Task ResendAsync(EmailRequest r, CancellationToken ct)
    {
        var u = await users.FindByEmailAsync(r.Email.Trim());
        if (u is { EmailConfirmed: false, IsActive: true }) await SendConfirmation(u, ct);
    }
    public async Task<SessionResult> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var normalized = users.NormalizeEmail(r.Email.Trim());
        var id = await db.Users.Where(x => x.NormalizedEmail == normalized).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (id is null) throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await LockUser(id.Value, ct) ?? throw AppException.Unauthorized();
        if (await users.IsLockedOutAsync(u)) throw AppException.Unauthorized();
        if (!await users.CheckPasswordAsync(u, r.Password))
        {
            Check(await users.AccessFailedAsync(u)); await tx.CommitAsync(ct); throw AppException.Unauthorized();
        }
        if (!u.IsActive || !u.EmailConfirmed) throw AppException.Unauthorized();
        Check(await users.ResetAccessFailedCountAsync(u));
        var result = Issue(u, Guid.NewGuid()); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
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
        if (old.ExpiresAtUtc <= Now || !u.IsActive || !(u.EmailConfirmed || u.PhoneNumberConfirmed)) throw AppException.Unauthorized();
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
    public async Task ForgotAsync(EmailRequest r, CancellationToken ct)
    {
        var u = await users.FindByEmailAsync(r.Email.Trim());
        if (u is not { EmailConfirmed: true, IsActive: true }) return;
        var token = Encode(await users.GeneratePasswordResetTokenAsync(u));
        await Send(u.Email!, "بازیابی رمز HireYar", Link("reset-password", u.Id, token), ct);
    }
    public async Task ResetAsync(ResetPasswordRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await LockUser(r.UserId, ct);
        if (u is not { IsActive: true, EmailConfirmed: true }) throw AppException.Invalid("invalid_token");
        CheckToken(await users.ResetPasswordAsync(u, Decode(r.Token), r.Password)); await Revoke(u.Id, null, ct); await tx.CommitAsync(ct);
    }
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await LockUser(userId, ct) ?? throw AppException.Unauthorized();
        CheckToken(await users.ChangePasswordAsync(u, r.CurrentPassword, r.NewPassword)); await Revoke(u.Id, null, ct); await tx.CommitAsync(ct);
    }
    public async Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken ct) => await
        (from u in db.Users.AsNoTracking()
         join o in db.Organizations.AsNoTracking() on u.OrganizationId equals o.Id
         where u.Id == userId
         select new CurrentUserDto(u.Id, u.Email, u.PhoneNumber, u.FullName, u.OrganizationId, o.Name, u.Role)).SingleOrDefaultAsync(ct) ?? throw AppException.Unauthorized();
    public async Task<CurrentUserDto> UpdateAsync(Guid userId, NameRequest r, CancellationToken ct)
    {
        var u = await users.FindByIdAsync(userId.ToString()) ?? throw AppException.Unauthorized();
        u.FullName = r.FullName.Trim(); u.UpdatedAtUtc = Now; Check(await users.UpdateAsync(u)); return await GetAsync(userId, ct);
    }
    private Task<ApplicationUser?> LockUser(Guid id, CancellationToken ct) => db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private Task<int> Revoke(Guid userId, Guid? family, CancellationToken ct) => db.RefreshTokens.Where(x => x.UserId == userId && (!family.HasValue || x.FamilyId == family) && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
    private SessionResult Issue(ApplicationUser u, Guid family)
    {
        var raw = TokenSecrets.Create(); var expires = Now.AddMinutes(Settings.AccessMinutes); var refreshExpires = Now.AddDays(Settings.RefreshDays);
        db.RefreshTokens.Add(new RefreshToken { UserId = u.Id, FamilyId = family, Hash = TokenSecrets.Hash(raw), CreatedAtUtc = Now, ExpiresAtUtc = refreshExpires });
        var claims = new[] { new Claim("sub", u.Id.ToString()), new Claim("org", u.OrganizationId.ToString()), new Claim("role", u.Role.ToString()), new Claim("jti", Guid.NewGuid().ToString()) };
        var jwt = new JwtSecurityToken(Settings.Issuer, Settings.Audience, claims, Now.UtcDateTime, expires.UtcDateTime, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, raw, refreshExpires);
    }
    private async Task<bool> SendConfirmation(ApplicationUser u, CancellationToken ct) => await Send(u.Email!, "تأیید ایمیل HireYar", Link("confirm-email", u.Id, Encode(await users.GenerateEmailConfirmationTokenAsync(u))), ct);
    private string Link(string path, Guid id, string token) => $"برای ادامه این لینک را باز کنید:\n{Settings.FrontendUrl.TrimEnd('/')}/{path}?userId={id}&token={Uri.EscapeDataString(token)}\nاگر این درخواست متعلق به شما نیست، آن را نادیده بگیرید.";
    private async Task<bool> Send(string recipient, string subject, string body, CancellationToken ct)
    {
        try { await email.SendAsync(recipient, subject, body, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { logger.LogWarning("Email delivery failed; retry is available. EventCode={EventCode}", "email_delivery_failed"); return false; }
    }
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string Decode(string token)
    { try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); } catch (FormatException) { throw AppException.Invalid("invalid_token"); } }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw AppException.Invalid("account_rejected"); }
    private static void CheckToken(IdentityResult result) { if (!result.Succeeded) throw AppException.Invalid("invalid_token_or_password"); }
}
