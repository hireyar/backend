namespace HireYar.Domain.Interviews;

public enum CandidateApplicationStatus { Invited, Interviewing, InterviewCompleted }
public sealed class CandidateApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid JobPositionId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public CandidateApplicationStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class Skill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public sealed class PositionSkill
{
    public Guid OrganizationId { get; set; }
    public Guid JobPositionId { get; set; }
    public Guid SkillId { get; set; }
    public bool IsRequired { get; set; }
    public int Priority { get; set; }
    public int TargetDifficulty { get; set; } = 3;
}
// Separate from editable template children; a reusable bank row maps explicitly to a position.
public sealed class BankQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid JobPositionId { get; set; }
    public Guid SkillId { get; set; }
    public string Text { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Difficulty { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class QuestionAudio
{
    public Guid OrganizationId { get; set; }
    public string AudioHash { get; set; } = "";
    public string AudioStorageKey { get; set; } = "";
    public string AudioModel { get; set; } = "";
    public string AudioVoice { get; set; } = "";
    public decimal AudioSpeed { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
