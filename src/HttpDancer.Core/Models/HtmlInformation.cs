using System.Text.Json.Serialization;

namespace HttpDancer.Core.Models;
public class HtmlInformation
{
    [JsonPropertyName("responseHeaders")] 
    // public List<KeyValuePair<string, string>> ResponseHeaders { get; set; } = [];
    public string ResponseHeaders { get; set; } = "";

    [JsonPropertyName("statusCode")] 
    public int StatusCode { get; set; } = 418;

    [JsonPropertyName("url")] 
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("meta")] 
    public MetaInformation Meta { get; set; } = new();
    
    [JsonPropertyName("internalHyperLinks")]
    public List<string> InternalHyperLinks { get; set; } = [];

    [JsonPropertyName("externalHyperLinks")]
    public List<string> ExternalHyperLinks { get; set; } = [];

    [JsonPropertyName("internalMediaLinks")]
    public List<string> InternalMediaLinks { get; set; } = [];

    [JsonPropertyName("externalMediaLinks")]
    public List<string> ExternalMediaLinks { get; set; } = [];
    
    [JsonPropertyName("fieName")]
    public string FileName { get; set; } = string.Empty;
}

public class MetaInformation
{
    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("canonical")]
    public string Canonical { get; set; } = string.Empty;

    [JsonPropertyName("robots")]
    public string Robots { get; set; } = string.Empty;

    [JsonPropertyName("additionalMeta")] 
    public Dictionary<string, string> AdditionalMeta { get; set; } = [];
}
