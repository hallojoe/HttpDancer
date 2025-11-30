using System.Text.Json.Serialization;

namespace HttpDancer.Core.Models;

public class MediaInformation
{
    [JsonPropertyName("responseHeaders")] 
    public string ResponseHeaders { get; set; } = "";
    
    [JsonPropertyName("contentType")] 
    public string ContentType { get; set; } = string.Empty;
    
    [JsonPropertyName("extension")] 
    public string Extension { get; set; } = string.Empty;
    
    [JsonPropertyName("filename")] 
    public string Filename { get; set; } = string.Empty;
    
    [JsonPropertyName("localFileLocation")] 
    public string LocalFileLocation { get; set; } = string.Empty;
    
    [JsonPropertyName("path")] 
    public string[] Path { get; set; } = [];

    [JsonPropertyName("statusCode")] 
    public int StatusCode { get; set; } = 418;

    [JsonPropertyName("url")] 
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("structuralUrl")] 
    public string StructualUrl { get; set; } = string.Empty;
}

