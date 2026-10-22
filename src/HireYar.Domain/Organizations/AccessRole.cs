namespace HireYar.Domain.Organizations;

public enum SectionAccess { None, Read, Write }
public sealed class AccessRole
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsAdministrator { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public List<RoleSection> Sections { get; set; } = [];
}
public sealed class AccessSection
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid Revision { get; set; } = Guid.NewGuid();
}
public sealed class RoleSection
{
    public string RoleCode { get; set; } = "";
    public string SectionCode { get; set; } = "";
    public SectionAccess Access { get; set; }
}
public sealed class AccessAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? SubjectId { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public static class AccessSections
{
    public static readonly string[] Codes = ["dashboard", "jobs", "templates", "questions", "interviews", "reports", "settings", "subscriptions", "tickets", "team"];
}

// Privileged assignment follows permissions, even if an operator clears the optional administrator label.
public static class AccessRoleRules
{
    public static bool RequiresOwnerAssignment(AccessRole role) => role.IsAdministrator || role.Sections.Any(x =>
        x.Access != SectionAccess.None && x.SectionCode is "team" or "settings" or "subscriptions");
}
