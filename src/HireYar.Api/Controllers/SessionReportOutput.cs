using System.Text.Json;
using System.Text.Json.Nodes;

namespace HireYar.Api.Controllers;

// Presentation boundary only: persisted policies and review snapshots retain their full data.
public static class SessionReportOutput
{
    public static JsonNode? Public<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Hide(node);
        return node;
    }

    private static void Hide(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var name in new[] { "positionFit", "fit", "policy", "weight", "weights", "weightSource", "formula", "passingScore", "strongScore", "minimumCoverage" })
                obj.Remove(name);
            foreach (var child in obj.ToArray()) Hide(child.Value);
        }
        else if (node is JsonArray array)
            foreach (var child in array) Hide(child);
    }
}
