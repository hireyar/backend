using System.Text.Json;
using HireYar.Api.Controllers;

namespace HireYar.Tests;

public sealed class SessionReportOutputTests
{
    [Fact]
    public void Public_output_exposes_current_position_fit_but_hides_policy_weights_and_historic_internal_fit_without_mutating_source()
    {
        var source = new { PositionFit = new { Value = 90 }, Questions = new[] { new { Text = "retained question" } }, Policy = new { PassingScore = 60, Weights = new[] { 20 } }, OverallScore = new { Value = 80, Formula = "weighted_mean" }, HumanReview = new { History = new[] { new { Snapshot = new { Policy = new { StrongScore = 80 }, Fit = new { Skills = new[] { new { Name = "C#", Weight = 20, Value = 75 } } } } } } } };
        var output = SessionReportOutput.Public(source)!.ToJsonString();
        Assert.DoesNotContain("policy", output);
        Assert.DoesNotContain("weight", output);
        Assert.DoesNotContain("formula", output);
        Assert.Contains("positionFit", output); Assert.Contains("90", output);
        Assert.DoesNotContain("\"fit\":", output);
        Assert.DoesNotContain("C#", output);
        Assert.Contains("retained question", output);
        Assert.Contains("Policy", JsonSerializer.Serialize(source));
    }
}
