using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobmatch.Json;

public static class JobmatchJsonOptions
{
    public static readonly JsonSerializerOptions Default = Build(indented: false);
    public static readonly JsonSerializerOptions Indented = Build(indented: true);

    private static JsonSerializerOptions Build(bool indented) => new()
    {
        WriteIndented = indented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
