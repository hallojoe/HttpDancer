namespace HttpDancer.Core.Naming.Query;

public interface IQueryStringProcessor
{
    QueryProcessingResult Process(Uri uri, UrlNamingOptions options);
}
