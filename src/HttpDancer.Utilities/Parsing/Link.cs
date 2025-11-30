namespace HttpDancer.Utilities.Parsing;

public class Link
{
    public required string SourceUrl { get; set; }
    public required string RawUrl { get; set; }
    public required Uri Uri { get; set; }
}