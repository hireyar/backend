using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Common;
using HireYar.Application.Sales;
using HireYar.Domain.Sales;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HireYar.Infrastructure.Sales;

public sealed class SalesAccounts(AppDbContext db, IOptions<SecurityOptions> security, TimeProvider time, IOptions<SalesAdminOptions> options) : ISalesAccounts
{
    private static readonly PasswordHasher<SalesAgent> Hasher = new();
    private static readonly SalesAgent Dummy = new();
    private static readonly string DummyHash = Hasher.HashPassword(Dummy, Guid.NewGuid().ToString());
    internal static SalesAgentDto Map(SalesAgent a) => new(a.Id,a.FullName,a.Mobile,a.IsActive,a.FirstSalePercent,a.RenewalPercent);
    public Task<SalesSessionDto> LoginAsync(SalesLoginRequest r, CancellationToken ct) => db.WithSalesLockAsync(async () =>
    {
        if (!options.Value.Enabled) throw AppException.NotFound();
        var now = time.GetUtcNow();
        var a = await db.SalesAgents.SingleOrDefaultAsync(x => x.Mobile == r.Mobile, ct);
        if (a is null || !a.IsActive || a.LockoutEndUtc > now) {
            Hasher.VerifyHashedPassword(Dummy,DummyHash,r.Password); throw AppException.Unauthorized();
        }
        if (Hasher.VerifyHashedPassword(a,a.PasswordHash,r.Password) == PasswordVerificationResult.Failed) {
            a.FailedAttempts++;
            if (a.FailedAttempts >= 5) { a.LockoutEndUtc = now.AddMinutes(15); a.FailedAttempts = 0; a.SecurityStamp = Guid.NewGuid(); }
            // Persist failures even though the request is rejected.
            await db.SaveChangesAsync(ct);
            return new SalesSessionDto("",now);
        }
        a.FailedAttempts = 0; a.LockoutEndUtc = null;
        var expires = now.AddMinutes(options.Value.AccessMinutes);
        var token = new JwtSecurityToken(security.Value.Issuer,security.Value.Audience+":sales",
            [new Claim("sub",a.Id.ToString()),new Claim("sales_stamp",a.SecurityStamp.ToString()),new Claim("sales_agent","true"),new Claim("sales_entry",options.Value.EntryFingerprint),new Claim("jti",Guid.NewGuid().ToString())],
            now.UtcDateTime,expires.UtcDateTime,new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.Value.SigningKey)),SecurityAlgorithms.HmacSha256));
        return new SalesSessionDto(new JwtSecurityTokenHandler().WriteToken(token),expires);
    },ct);
    public async Task<SalesAgentDto?> ValidateAsync(Guid id, Guid stamp, CancellationToken ct) {
        if (!options.Value.Enabled) return null;
        var now = time.GetUtcNow();
        var a = await db.SalesAgents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SecurityStamp == stamp && x.IsActive && (x.LockoutEndUtc == null || x.LockoutEndUtc <= now), ct);
        return a is null ? null : Map(a);
    }
    public async Task LogoutAsync(Guid id, CancellationToken ct) => await db.WithSalesLockAsync(async () => {
        var a = await db.SalesAgents.SingleOrDefaultAsync(x => x.Id == id,ct) ?? throw AppException.Unauthorized();
        a.SecurityStamp = Guid.NewGuid(); return true;
    },ct);
    public async Task<Page<SalesAgentDto>> ListAsync(PageQuery page, CancellationToken ct) {
        var q = db.SalesAgents.AsNoTracking(); var count = await q.CountAsync(ct);
        var rows = await q.OrderBy(x=>x.FullName).ThenBy(x=>x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(),count,page.Page,page.PageSize);
    }
    private static void Apply(SalesAgent a, SalesAgentRequest r) {
        SalesService.Text(r.FullName,200); SalesService.Rate(r.FirstSalePercent); SalesService.Rate(r.RenewalPercent);
        a.FullName=r.FullName.Trim(); a.Mobile=r.Mobile; a.IsActive=r.IsActive; a.FirstSalePercent=r.FirstSalePercent; a.RenewalPercent=r.RenewalPercent;
    }
    private void Audit(Guid actor,string action) => db.SalesAudits.Add(new() { ActorId=actor,Action=action,CreatedAtUtc=time.GetUtcNow() });
    private static string Password(SalesAgent a) {
        var password = "Hy!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(16))+"a7";
        a.PasswordHash=Hasher.HashPassword(a,password); a.SecurityStamp=Guid.NewGuid(); a.FailedAttempts=0; a.LockoutEndUtc=null;
        return password;
    }
    public Task<SalesCredentialsDto> CreateAsync(Guid actor,SalesAgentRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        if (await db.SalesAgents.AnyAsync(x=>x.Mobile==r.Mobile,ct)) throw new AppException(409,"sales_mobile_taken","Mobile already assigned.");
        var a = new SalesAgent { CreatedAtUtc=time.GetUtcNow() }; Apply(a,r); var password=Password(a);
        db.SalesAgents.Add(a); Audit(actor,"AgentCreated:"+a.Id); return new SalesCredentialsDto(Map(a),password);
    },ct);
    public Task<SalesAgentDto> UpdateAsync(Guid actor,Guid id,SalesAgentRequest r,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        var a=await db.SalesAgents.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw AppException.NotFound();
        if(await db.SalesAgents.AnyAsync(x=>x.Mobile==r.Mobile && x.Id!=id,ct)) throw new AppException(409,"sales_mobile_taken","Mobile already assigned.");
        Apply(a,r); a.SecurityStamp=Guid.NewGuid(); Audit(actor,"AgentUpdated:"+a.Id); return Map(a);
    },ct);
    public Task<SalesCredentialsDto> ResetPasswordAsync(Guid actor,Guid id,CancellationToken ct) => db.WithSalesLockAsync(async () => {
        var a=await db.SalesAgents.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw AppException.NotFound();
        var password=Password(a); Audit(actor,"PasswordReset:"+a.Id); return new SalesCredentialsDto(Map(a),password);
    },ct);
}

public sealed class SalesLeads(AppDbContext db, TimeProvider time) : ISalesLeads
{
    public async Task<SalesLeadDto> CreateAsync(Guid agent,SalesLeadRequest r,CancellationToken ct) {
        SalesService.Text(r.CustomerName,200); SalesService.Text(r.Note,2000,false);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await TeamMembershipLocks.LockMobileAsync(db,r.Mobile,ct);
        var result=await db.WithSalesLockAsync(async () => {
            if (!await db.SalesAgents.AnyAsync(x=>x.Id==agent && x.IsActive,ct)) throw AppException.Unauthorized();
            var existingLead = await db.SalesLeads.Where(x=>x.Mobile==r.Mobile).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (existingLead is not null) {
                db.SalesAudits.Add(new() { ActorId=agent, LeadId=existingLead, Action="DuplicateClaimRejected",CreatedAtUtc=time.GetUtcNow() });
                return (SalesLeadDto?)null;
            }
            var customer=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.PhoneNumber==r.Mobile,ct);
            if (customer is not null && customer.Role != HireYar.Domain.Organizations.OrganizationRole.Owner)
                throw new AppException(409,"lead_unavailable","Mobile cannot be attributed.");
            if (customer is not null && await db.SalesLeads.AnyAsync(x=>x.OrganizationId==customer.OrganizationId,ct))
                throw new AppException(409,"lead_unavailable","Customer already attributed.");
            var lead=new SalesLead { AgentId=agent,Mobile=r.Mobile,CustomerName=r.CustomerName.Trim(),Note=r.Note.Trim(),
                CreatedAtUtc=time.GetUtcNow(),LastContactAtUtc=time.GetUtcNow(),OrganizationId=customer?.OrganizationId,RegisteredAtUtc=customer?.CreatedAtUtc };
            db.SalesLeads.Add(lead);
            db.SalesAudits.Add(new() { ActorId=agent,LeadId=lead.Id,Action=customer is null ? "LeadCreated" : "ExistingCustomerClaimed",CreatedAtUtc=time.GetUtcNow() });
            var won=customer is not null && await db.SubscriptionOrders.AnyAsync(x=>x.OrganizationId==customer.OrganizationId && x.Product=="subscription" && x.Status=="Confirmed",ct);
            return SalesService.MapLead(lead,won,time.GetUtcNow());
        },ct);
        await tx.CommitAsync(ct);
        return result ?? throw new AppException(409,"lead_already_claimed","Mobile already claimed.");
    }
}
