namespace HireYar.Domain.Billing;

public sealed class InterviewTypeDefinition
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    // Execution strategies are code; the catalog and prices are data.
    public string EngineType { get; set; } = "general";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PackageInterviewType
{
    public Guid PackageId { get; set; }
    public string InterviewTypeCode { get; set; } = "";
    public InterviewTypeDefinition InterviewType { get; set; } = null!;
    public decimal PricePerMinuteUsd { get; set; }
    public bool IsActive { get; set; }
}
