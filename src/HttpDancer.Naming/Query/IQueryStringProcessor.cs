namespace HttpDancer.Naming.Query;

public interface IQueryStringProcessor
{
    QueryProcessingResult Process(Uri uri, UrlNamingOptions options);
}
