namespace HireYar.Tests;

internal static class TestJson
{
    public static System.Text.Json.JsonSerializerOptions Options { get; } = Create();
    private static System.Text.Json.JsonSerializerOptions Create()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)); return options;
    }
}

