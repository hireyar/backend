using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

public sealed class AccessRoleConfiguration : IEntityTypeConfiguration<AccessRole>
{
    public void Configure(EntityTypeBuilder<AccessRole> b)
    {
        b.HasKey(x => x.Code); b.Property(x => x.Code).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasMany(x => x.Sections).WithOne().HasForeignKey(x => x.RoleCode);
    }
}
public sealed class AccessSectionConfiguration : IEntityTypeConfiguration<AccessSection>
{
    public void Configure(EntityTypeBuilder<AccessSection> b)
    {
        b.HasKey(x => x.Code); b.Property(x => x.Code).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(100); b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Revision).IsConcurrencyToken();
    }
}
public sealed class RoleSectionConfiguration : IEntityTypeConfiguration<RoleSection>
{
    public void Configure(EntityTypeBuilder<RoleSection> b)
    {
        b.HasKey(x => new { x.RoleCode, x.SectionCode });
        b.Property(x => x.RoleCode).HasMaxLength(64); b.Property(x => x.SectionCode).HasMaxLength(64);
        b.HasOne<AccessSection>().WithMany().HasForeignKey(x => x.SectionCode);
        b.Property(x => x.Access).HasConversion<string>().HasMaxLength(10);
        b.ToTable(t => t.HasCheckConstraint("CK_RoleSection_Access", "\"Access\" IN ('None','Read','Write')"));
    }
}
public sealed class AccessUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.AccessRoleCode).HasMaxLength(64);
        b.HasOne<AccessRole>().WithMany().HasForeignKey(x => x.AccessRoleCode);
    }
}
public sealed class AccessInvitationConfiguration : IEntityTypeConfiguration<MobileTeamInvitation>
{
    public void Configure(EntityTypeBuilder<MobileTeamInvitation> b)
    {
        b.Property(x => x.AccessRoleCode).HasMaxLength(64).HasDefaultValue("interview_manager");
        b.HasOne<AccessRole>().WithMany().HasForeignKey(x => x.AccessRoleCode);
        b.Property(x => x.Email).HasMaxLength(256); b.Property(x => x.Language).HasMaxLength(2);
        b.Property(x => x.Message).HasMaxLength(2000);
    }
}
public sealed class AccessAuditConfiguration : IEntityTypeConfiguration<AccessAudit>
{
    public void Configure(EntityTypeBuilder<AccessAudit> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(64); b.Property(x => x.Detail).HasMaxLength(20000);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
    }
}
