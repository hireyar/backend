using System.Text.Json;
using HireYar.Api.Controllers;

namespace HireYar.Tests;

public sealed class SessionReportOutputTests
{
    [Fact]
    public void Public_output_hides_policy_and_weights_including_historical_snapshots_without_mutating_source()
    {
        var source = new { Policy = new { PassingScore = 60, Weights = new[] { 20 } }, OverallScore = new { Value = 80, Formula = "weighted_mean" }, HumanReview = new { History = new[] { new { Snapshot = new { Policy = new { StrongScore = 80 }, Fit = new { Skills = new[] { new { Name = "C#", Weight = 20, Value = 75 } } } } } } } };
        var output = SessionReportOutput.Public(source)!.ToJsonString();
        Assert.DoesNotContain("policy", output);
        Assert.DoesNotContain("weight", output);
        Assert.DoesNotContain("formula", output);
        Assert.Contains("C#", output);
        Assert.Contains("75", output);
        Assert.Contains("Policy", JsonSerializer.Serialize(source));
    }
}
