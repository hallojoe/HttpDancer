namespace HttpDancer.Core.Http;

public static class HttpConstants
{
    public const string DefaultContentType = "application/octet-stream";
    public static readonly string[] MethodsWithoutExpectedResponseBody = ["HEAD", "TRACE", "CONNECT"];

}