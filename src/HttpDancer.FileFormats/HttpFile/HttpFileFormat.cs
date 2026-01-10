// HttpFileParserRenderer.cs
// .NET 9/10-friendly, no external deps.
// Goal: parse a '.http' file into request(s) and render "nice output" (not round-trip exact formatting).

using System.Text;
using System.Text.RegularExpressions;
using HttpDancer.Parsing;

namespace HttpDancer.FileFormats.HttpFile;

/// <summary>
/// A parsed '.http' file: variables and a list of requests.
/// </summary>
public sealed record HttpFileDocument(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<HttpRequestDefinition> Requests);

/// <summary>
/// A parsed request block from a '.http' file.
/// </summary>
public sealed record HttpRequestDefinition(
    string? Name,
    HttpMethod Method,
    Uri Url,
    IReadOnlyList<HttpHeader> Headers,
    string? Body);

/// <summary>
/// Represents a header line. We use a list to allow duplicates (e.g., Set-Cookie).
/// </summary>
public sealed record HttpHeader(string Name, string Value);

public sealed record HttpFileParseOptions(
    bool ResolveVariables = false,
    bool AllowMissingSchemeOnUrl = false,
    Uri? DefaultBaseUri = null);

public sealed record HttpFileRenderOptions(
    bool EmitVariablesHeader = true,
    bool PrettyPrintJsonBodies = true);

public interface IHttpFileParser
{
    /// <summary>Parse a '.http' file content already loaded as text.</summary>
    HttpFileDocument Parse(string text, HttpFileParseOptions? options = null);

    /// <summary>
    /// Parse links from any textual source. And create a '.http' file document.  
    /// </summary>
    HttpFileDocument Parse(string text, string baseUrl, string method, HttpFileParseOptions? options = null);

    /// <summary>Parse a UTF-8 stream containing '.http' file content.</summary>
    Task<HttpFileDocument> ParseAsync(Stream utf8Stream, HttpFileParseOptions? options = null, CancellationToken cancellationToken = default);
}

public interface IHttpFileRenderer
{
    /// <summary>Render a document to a '.http' file string.</summary>
    string Render(HttpFileDocument document, HttpFileRenderOptions? options = null);

    /// <summary>Render a single request to a '.http' request block string.</summary>
    string Render(HttpRequestDefinition request, HttpFileRenderOptions? options = null);
}

/// <summary>
/// Lightweight '.http' parser for common JetBrains / VS / REST Client style:
/// - Variables:   @name = value
/// - Request sep: ### (optionally "### Name")
/// - Request line: METHOD URL
/// - Headers:    Key: Value
/// - Body:       everything after blank line until next ### or EOF
/// This is not a full spec implementation; it targets the 80/20.
/// </summary>
public sealed partial class HttpFileParser(ILinkParser linkParser) : IHttpFileParser
{
    private static readonly Regex VariableExpression = VariableRegularExpression();
    private static readonly Regex SeparatorExpression = SeparatorRegularExpression();
    private static readonly Regex RequestLineExpression = RequestLineRegularExpression();

    public HttpFileDocument Parse(string text, HttpFileParseOptions? options = null)
    {
        options ??= new HttpFileParseOptions();
        
        // Normalize newlines for simpler parsing.
        var lines = SplitLines(text);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var requests = new List<HttpRequestDefinition>();

        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];

            // Variables can appear anywhere, but in practice they are near the top.
            var varMatch = VariableExpression.Match(line);
            if (varMatch.Success)
            {
                var name = varMatch.Groups["name"].Value;
                var value = varMatch.Groups["value"].Value.Trim();
                variables[name] = value;
                i++;
                continue;
            }

            // Skip empty lines and "comment" lines (common forms).
            if (IsIgnorableLine(line))
            {
                i++;
                continue;
            }

            // Seek to start of request: either separator or direct request line.
            string? requestName = null;
            var sepMatch = SeparatorExpression.Match(line);
            if (sepMatch.Success)
            {
                requestName = sepMatch.Groups["name"].Value.Trim();
                if (string.IsNullOrWhiteSpace(requestName))
                {
                    requestName = null;
                }

                i++;

                // Skip ignorable lines after ###
                while (i < lines.Count && IsIgnorableLine(lines[i]))
                {
                    i++;
                }
            }

            if (i >= lines.Count)
            {
                break;
            }

            // Parse request line
            // Parse request line
            var reqLine = lines[i];
            var reqMatch = RequestLineExpression.Match(reqLine);
            if (!reqMatch.Success)
            {
                // Not a request we understand; skip line (keeps parser resilient on invalid reads).
                i++;
                continue;
            }

            var method = new HttpMethod(reqMatch.Groups["method"].Value.ToUpperInvariant());
            var urlRaw = reqMatch.Groups["url"].Value;
            i++;

            // Headers until blank line (body delimiter) OR separator OR next request line OR EOF
            var headers = new List<HttpHeader>();
            var bodyCanStart = false;

            while (i < lines.Count)
            {
                var hLine = lines[i];

                // Next request separator => end this request
                if (SeparatorExpression.IsMatch(hLine))
                {
                    break;
                }

                // If we see another "METHOD URL" line, it's a new request (when no blank line was used)
                if (RequestLineExpression.IsMatch(hLine))
                {
                    break;
                }

                // Blank line means: headers ended, body may start
                if (string.IsNullOrWhiteSpace(hLine))
                {
                    bodyCanStart = true;
                    i++; // consume blank line before body
                    break;
                }

                // Ignore line comments in the header area
                if (IsIgnorableLine(hLine))
                {
                    i++;
                    continue;
                }

                // Header format: Name: Value
                var colonIdx = hLine.IndexOf(':');
                if (colonIdx > 0)
                {
                    var hName = hLine[..colonIdx].Trim();
                    var hValue = hLine[(colonIdx + 1)..].Trim();
                    headers.Add(new HttpHeader(hName, hValue));
                    i++;
                    continue;
                }

                // Not a header, not blank, not comment => treat as "no-body" and let outer loop re-process
                break;
            }

            // Body only if we consumed a blank line delimiter
            string? body = null;
            if (bodyCanStart)
            {
                var bodySb = new StringBuilder();
                while (i < lines.Count)
                {
                    var bLine = lines[i];
                    if (SeparatorExpression.IsMatch(bLine))
                    {
                        break;          // next request block
                    }

                    if (RequestLineExpression.IsMatch(bLine))
                    {
                        break;  // next request without ###
                    }

                    bodySb.AppendLine(bLine);
                    i++;
                }

                body = bodySb.Length == 0 ? null : TrimTrailingNewlines(bodySb.ToString());
            }

            // Variable substitution (optional)
            if (options.ResolveVariables && variables.Count > 0)
            {
                urlRaw = HttpFileVariables.Apply(urlRaw, variables);
                headers = headers
                    .Select(h => new HttpHeader(h.Name, HttpFileVariables.Apply(h.Value, variables)))
                    .ToList();

                if (body is not null)
                {
                    body = HttpFileVariables.Apply(body, variables);
                }
            }

            // Allow relative URLs when @baseUrl exists (even if ResolveVariables == false).
            var effectiveBaseUri = options.DefaultBaseUri;

            if (effectiveBaseUri is null
                && variables.TryGetValue("baseUrl", out var baseUrlValue)
                && Uri.TryCreate(baseUrlValue, UriKind.Absolute, out var parsedBaseUrl))
            {
                effectiveBaseUri = parsedBaseUrl;
            }

            var url = ParseUri(urlRaw, options with { DefaultBaseUri = effectiveBaseUri });
            
            requests.Add(new HttpRequestDefinition(
                Name: requestName,
                Method: method,
                Url: url,
                Headers: headers,
                Body: body));
        }

        return new HttpFileDocument(variables, requests);
    }

    public HttpFileDocument Parse(string text, string baseUrl, string method, HttpFileParseOptions? options = null)
    {
        var baseUri = new Uri(baseUrl);
        var linkCollection = linkParser.GetLinks(text, baseUrl);
        var httpFileContents = new StringBuilder($"@baseUrl = {baseUrl}").AppendLine();
        
        var distinctRequestCollection = new HashSet<string>();
        
        foreach (var uri in linkCollection.Select(x => x.Uri))
        {
            if(uri.Host != baseUri.Host) continue;
            
            distinctRequestCollection.Add($"{method} {uri}");
        }

        foreach (var request in distinctRequestCollection)
        {
            httpFileContents.AppendLine(request);
        }
        
        return Parse(httpFileContents.ToString(), options);
    }

    public async Task<HttpFileDocument> ParseAsync(Stream utf8Stream, HttpFileParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new HttpFileParseOptions();

        using var reader = new StreamReader(utf8Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Parse(text, options);
    }
    
    private static Uri ParseUri(string urlRaw, HttpFileParseOptions options)
    {
        // Absolute URI → done
        if (Uri.TryCreate(urlRaw, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        // Relative URI + baseUrl variable (preferred)
        if (options.DefaultBaseUri is not null &&
            Uri.TryCreate(options.DefaultBaseUri, urlRaw, out var combined))
        {
            return combined;
        }

        // Allow scheme-less host (e.g. api.example.com/path)
        if (options.AllowMissingSchemeOnUrl &&
            Uri.TryCreate("https://" + urlRaw.TrimStart('/'), UriKind.Absolute, out var guessed))
        {
            return guessed;
        }

        throw new FormatException(
            $"Relative URL '{urlRaw}' requires DefaultBaseUri or @baseUrl variable.");
    }

    private static bool IsIgnorableLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var trimmed = line.TrimStart();
        // Common comment forms in '.http' files:
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static List<string> SplitLines(string text)
    {
        // Keep empty lines. Normalize to '\n' and split.
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).ToList();
    }

    private static string TrimTrailingNewlines(string s)
        => s.TrimEnd('\n', '\r');
    [GeneratedRegex(@"^\s*###(?<name>.*)?$", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegularExpression();
    [GeneratedRegex(@"^\s*(?<method>[A-Za-z]+)\s+(?<url>\S+)\s*(?:HTTP\/\d(?:\.\d)?)?\s*$", RegexOptions.Compiled)]
    private static partial Regex RequestLineRegularExpression();
    [GeneratedRegex(@"^\s*@(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)\s*$", RegexOptions.Compiled)]
    private static partial Regex VariableRegularExpression();
}

/// <summary>
/// Renderer that outputs a clean, readable '.http' file format.
/// </summary>
public sealed class HttpFileRenderer : IHttpFileRenderer
{
    public string Render(HttpFileDocument document, HttpFileRenderOptions? options = null)
    {
        options ??= new HttpFileRenderOptions();

        var stringBuilder = new StringBuilder();

        if (options.EmitVariablesHeader && document.Variables.Count > 0)
        {
            foreach (var kvp in document.Variables.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                stringBuilder
                    .Append('@')
                    .Append(kvp.Key).Append(" = ")
                    .Append(kvp.Value)
                    .AppendLine();
            }
        }
        
        foreach (var t in document.Requests)
        {
            stringBuilder.Append(Render(t, options));
        }

        stringBuilder.AppendLine();

        return stringBuilder.ToString();
    }

    public string Render(HttpRequestDefinition request, HttpFileRenderOptions? options = null)
    {
        options ??= new HttpFileRenderOptions();

        var stringBuilder = new StringBuilder();

        // Separator / name
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            stringBuilder.Append("###");
            stringBuilder.Append(' ').Append(request.Name!.Trim());
        }

        stringBuilder.AppendLine();

        // Request line
        stringBuilder.Append(request.Method.Method.ToUpperInvariant())
          .Append(' ')
          .Append(request.Url)
          .AppendLine();

        // Headers
        foreach (var httpHeader in request.Headers)
        {
            stringBuilder.Append(httpHeader.Name).Append(": ").Append(httpHeader.Value).AppendLine();
        }

        // Body
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            stringBuilder.AppendLine();

            var body = request.Body!;
            if (options.PrettyPrintJsonBodies && LooksLikeJson(body) && TryPrettyJson(body, out var pretty))
            {
                body = pretty;
            }

            stringBuilder.Append(body).AppendLine();
        }

        return stringBuilder.ToString().TrimEnd(); // keep blocks tight; caller adds spacing between blocks
    }

    private static bool LooksLikeJson(string utf8EncodedString)
    {
        var potentialJsonString = utf8EncodedString.TrimStart();
        return potentialJsonString.StartsWith("{", StringComparison.Ordinal) || potentialJsonString.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool TryPrettyJson(string utf8EncodedJsonString, out string prettyJsonString)
    {
        try
        {
            // System.Text.Json is part of the BCL.
            using var jsonDocument = System.Text.Json.JsonDocument.Parse(utf8EncodedJsonString);
            prettyJsonString = System.Text.Json.JsonSerializer.Serialize(jsonDocument.RootElement, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            return true;
        }
        catch
        {
            prettyJsonString = utf8EncodedJsonString;
            return false;
        }
    }
}

/// <summary>
/// Variable interpolation helper: replaces {{name}} with values.
/// Keeps unknown variables unchanged.
/// </summary>
public static class HttpFileVariables
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.Compiled);

    public static string Apply(string input, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input) || variables.Count == 0)
        {
            return input;
        }

        return TokenRegex.Replace(input, m =>
        {
            var name = m.Groups["name"].Value;
            return variables.TryGetValue(name, out var value) ? value : m.Value;
        });
    }
}

/// <summary>
/// Optional helpers if you want to execute parsed requests via HttpClient.
/// </summary>
public static class HttpRequestDefinitionExtensions
{
    /// <summary>
    /// Convert to HttpRequestMessage. If Body is present and no Content-Type header exists,
    /// you may want to add one yourself before sending.
    /// </summary>
    public static HttpRequestMessage ToHttpRequestMessage(this HttpRequestDefinition req)
    {
        var msg = new HttpRequestMessage(req.Method, req.Url);

        // Copy headers. If a header belongs to content headers, we'll handle it after content is set.
        foreach (var h in req.Headers)
        {
            // Try to add as request header first; if it fails, we'll add to content headers later.
            msg.Headers.TryAddWithoutValidation(h.Name, h.Value);
        }

        if (string.IsNullOrWhiteSpace(req.Body))
        {
            return msg;
        }

        // Default to UTF-8 string content. Headers should drive Content-Type if present.
        msg.Content = new StringContent(req.Body!, Encoding.UTF8);

        // Some headers might have been intended for content headers (e.g., Content-Type).
        foreach (var h in req.Headers)
        {
            if (!msg.Headers.Contains(h.Name))
            {
                msg.Content.Headers.TryAddWithoutValidation(h.Name, h.Value);
            }
        }
        
        return msg;
    }
}
