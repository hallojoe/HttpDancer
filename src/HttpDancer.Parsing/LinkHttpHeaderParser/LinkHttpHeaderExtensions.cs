namespace HttpDancer.Parsing.LinkHttpHeaderParser;

public static class LinkHttpHeaderExtensions
{
    private static bool IsRelWithName(this NamedString attribute, string name)
    {
        return attribute.Name.Equals("rel", StringComparison.OrdinalIgnoreCase) && attribute.Value.Equals(name, StringComparison.OrdinalIgnoreCase);
    }
    
    public static string? GetRelCanonical(this IReadOnlyList<NamedString> attributes)
    {
        return attributes.FirstOrDefault(namedString => namedString.IsRelWithName("canonical")).Value;
    }
    
    public static string? GetRelFirst(this IReadOnlyList<NamedString> attributes)
    {
        return attributes.FirstOrDefault(namedString => namedString.IsRelWithName("first")).Value;
    }
    
    public static string? GetRelLast(this IReadOnlyList<NamedString> attributes)
    {
        return attributes.FirstOrDefault(namedString => namedString.IsRelWithName("last")).Value;
    }
    
    public static string? GetRelNext(this IReadOnlyList<NamedString> attributes)
    {
        return attributes.FirstOrDefault(namedString => namedString.IsRelWithName("next")).Value;
    }
    
    public static string? GetRelPrev(this IReadOnlyList<NamedString> attributes)
    {
        return attributes.FirstOrDefault(namedString => namedString.IsRelWithName("prev")).Value;
    }
}