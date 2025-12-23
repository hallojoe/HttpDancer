using System.Text.Json.Serialization;

namespace HttpDancer.Html;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Flags]
public enum HtmlValueInstructions
{
    None = 0,
    Trim = 1,
    ParseDate = 2,
    RemoveAttributes = 4,
    AbsoluteUrls = 8
}