using System.Text.Json.Serialization;

namespace HttpDancer.Html;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HtmlValueStrategy
{
    OuterHtml,
    InnerHtml,
    Text,
    Attribute
}