using System.Text.Json;
using System.Text.Json.Serialization;

namespace CsoKit.Cli.Commands;

public static class JsonConsole
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static void Write(object value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, Options));
    }
}
