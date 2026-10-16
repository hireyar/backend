using System.ComponentModel.DataAnnotations;

namespace HireYar.Application.Common;

public sealed class SearchQuery
{
    [StringLength(256)] public string? Search { get; set; }
}
