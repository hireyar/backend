using System.ComponentModel.DataAnnotations;

namespace HireYar.Application.Billing;

public sealed record InterviewTypeRequest(
    [Required, RegularExpression("^[a-z][a-z0-9_]{1,63}$")] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, RegularExpression("^(general|specialized)$")] string EngineType,
    bool IsActive, [Range(0, 10000)] int SortOrder = 0);
public sealed record InterviewTypeUpdateRequest(InterviewTypeRequest Type,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record InterviewTypeDto(string Code, string Name, string EngineType, bool IsActive, int SortOrder, byte[] RowVersion);
public sealed record PackageInterviewTypeRequest(
    [Range(typeof(decimal), "0.000001", "1000")] decimal PricePerMinuteUsd, bool IsActive,
    [Required, MinLength(16), MaxLength(16)] byte[] RowVersion);
public sealed record PackageInterviewTypeDto(string Code, string Name, string EngineType, bool IsActive,
    bool TypeIsActive, decimal PricePerMinuteUsd);
