using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Auth;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HireYar.Infrastructure.Auth;

public sealed class AdminAccounts(AppDbContext db, UserManager<ApplicationUser> users, IAdminCaptcha captcha,
    IOptions<SecurityOptions> security, IOptions<SuperAdminOptions> admin, TimeProvider time) : IAdminAccounts
{
    private static readonly ApplicationUser Dummy = new();
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(Dummy, Guid.NewGuid().ToString("N"));

    public async Task<AdminSessionDto> LoginAsync(AdminLoginRequest r, CancellationToken ct)
    {
        if (!admin.Value.Enabled) throw AppException.NotFound();
        if (!captcha.Consume(r.CaptchaId, r.CaptchaAnswer)) throw AppException.Invalid("invalid_captcha");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize password failures and session revocation for this account.
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"PhoneNumber\" = {r.Mobile} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (u is not { IsActive: true, IsSystemAdmin: true, PhoneNumberConfirmed: true, Role: OrganizationRole.Owner } || await users.IsLockedOutAsync(u))
        {
            Hasher.VerifyHashedPassword(Dummy, DummyHash, r.Password);
            throw AppException.Unauthorized();
        }
        if (!await users.CheckPasswordAsync(u, r.Password))
        {
            Check(await users.AccessFailedAsync(u));
            await tx.CommitAsync(ct);
            throw AppException.Unauthorized();
        }
        Check(await users.ResetAccessFailedCountAsync(u));
        var now = time.GetUtcNow(); var expires = now.AddMinutes(admin.Value.AccessMinutes);
        var claims = new[] { new Claim("sub", u.Id.ToString()), new Claim("jti", Guid.NewGuid().ToString()),
            new Claim("admin_stamp", u.SecurityStamp!), new Claim("admin_entry", admin.Value.EntryFingerprint), new Claim("system_admin", "true") };
        var token = new JwtSecurityToken(security.Value.Issuer, security.Value.Audience + ":super-admin", claims, now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.Value.SigningKey)), SecurityAlgorithms.HmacSha256));
        await tx.CommitAsync(ct);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
    public async Task<AdminIdentityDto?> ValidateAsync(Guid userId, string stamp, CancellationToken ct)
    {
        if (!admin.Value.Enabled) return null;
        var now = time.GetUtcNow();
        return await db.Users.AsNoTracking().Where(x => x.Id == userId && x.IsActive && x.IsSystemAdmin && x.PhoneNumberConfirmed &&
            x.Role == OrganizationRole.Owner && x.SecurityStamp == stamp && (!x.LockoutEnabled || x.LockoutEnd == null || x.LockoutEnd <= now))
            .Select(x => new AdminIdentityDto(x.Id, x.FullName)).SingleOrDefaultAsync(ct);
    }
    public async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw AppException.Unauthorized();
        Check(await users.UpdateSecurityStampAsync(u));
        await tx.CommitAsync(ct);
    }
    public async Task<AdminGrantDto> GrantAsync(AdminGrantRequest r, CancellationToken ct)
    {
        if (!admin.Value.Enabled) throw AppException.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"PhoneNumber\" = {r.Mobile} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound();
        if (u is not { IsActive: true, PhoneNumberConfirmed: true, Role: OrganizationRole.Owner })
            throw new AppException(400, "admin_account_ineligible", "حساب باید Owner فعال با موبایل تأییدشده باشد.");
        if (u.IsSystemAdmin) throw new AppException(409, "already_super_admin", "این حساب از قبل سوپرادمین است.");
        var password = "Hy!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) + "a7";
        await SetPasswordAndAdminAsync(u, password);
        await tx.CommitAsync(ct);
        return new(u.Id, u.FullName, r.Mobile, password);
    }

    // Bootstrap/reset command for the local operator; HTTP grants never reset existing admins.
    public async Task ProvisionAsync(string mobile, string password, CancellationToken ct)
    {
        if (password.Length is < 12 or > 128) throw new InvalidOperationException("Password must contain 12 to 128 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"PhoneNumber\" = {mobile} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (u is not { IsActive: true, PhoneNumberConfirmed: true, Role: OrganizationRole.Owner }) throw new InvalidOperationException("An existing active, phone-confirmed Owner account is required.");
        await SetPasswordAndAdminAsync(u, password);
        await tx.CommitAsync(ct);
    }
    private async Task SetPasswordAndAdminAsync(ApplicationUser u, string password)
    {
        if (await users.HasPasswordAsync(u)) Check(await users.RemovePasswordAsync(u));
        Check(await users.AddPasswordAsync(u, password));
        u.IsSystemAdmin = true; u.LockoutEnabled = true; u.LockoutEnd = null; u.AccessFailedCount = 0;
        Check(await users.UpdateAsync(u));
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Identity rejected the operation: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
