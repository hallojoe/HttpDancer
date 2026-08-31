using System.Text.Json;
using HttpDancer.Core.Http.Clients;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.KnownMediaTypes;
using HttpDancer.Naming;

namespace HttpDancer.Downloading;

internal sealed class FileSystemRunArchive
{
    private readonly string _contentDirectory;
    private readonly IUrlNamer _urlNamer;
    private readonly IKnowMediaTypes _mediaTypes;

    private FileSystemRunArchive(string runDirectory, IUrlNamer urlNamer, IKnowMediaTypes mediaTypes)
    {
        RunDirectory = runDirectory;
        _contentDirectory = Path.Combine(runDirectory, "content");
        _urlNamer = urlNamer;
        _mediaTypes = mediaTypes;
        Directory.CreateDirectory(_contentDirectory);
    }

    public string RunDirectory { get; }

    public static FileSystemRunArchive Create(string workspace, Uri seedUri, IUrlNamer urlNamer, IKnowMediaTypes mediaTypes)
    {
        var resolvedWorkspace = ResolveWorkspace(workspace);
        var safeHost = string.Concat(seedUri.Host.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var runId = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}"[..33];
        var runDirectory = Path.Combine(resolvedWorkspace, safeHost, runId);
        Directory.CreateDirectory(runDirectory);
        return new FileSystemRunArchive(runDirectory, urlNamer, mediaTypes);
    }

    public Task WriteManifestAsync(string filename, HttpFileDocument document, IHttpFileRenderer renderer, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(
            Path.Combine(RunDirectory, filename),
            renderer.Render(document, new HttpFileRenderOptions(true, false)),
            cancellationToken);

    public async Task WriteResponseAsync(CompletedHttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fileStem = GetFileStem(response.Uri ?? response.SerializableRequest.Uri, response.ContentType);
        var metadataPath = Path.Combine(_contentDirectory, $"{fileStem}.meta.json");
        EnsureUnderContentDirectory(metadataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);

        var metadata = SerializeMetadata(response);
        await File.WriteAllTextAsync(metadataPath, metadata, cancellationToken).ConfigureAwait(false);

        if (response.BodyBytes is not { Length: > 0 }) return;

        var bodyPath = Path.Combine(_contentDirectory, fileStem);
        EnsureUnderContentDirectory(bodyPath);
        await File.WriteAllBytesAsync(bodyPath, response.BodyBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteSeedFailureAsync(CompletedHttpResponseMessage response, CancellationToken cancellationToken)
    {
        var metadata = SerializeMetadata(response);
        await File.WriteAllTextAsync(Path.Combine(RunDirectory, "seed.meta.json"), metadata, cancellationToken)
            .ConfigureAwait(false);
    }

    private string GetFileStem(Uri uri, string? contentType)
    {
        var nameAndPath = _urlNamer.GetNameAndPath(uri);
        var segments = (nameAndPath.FullPath ?? nameAndPath.Name)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_'))
            .ToArray();
        var relativePath = segments.Length == 0 ? "index" : Path.Combine(segments);
        return $"{relativePath}.{_mediaTypes.GetExtension(contentType)}";
    }

    private static string SerializeMetadata(CompletedHttpResponseMessage response) =>
        JsonSerializer.Serialize(new
        {
            Url = response.Url,
            response.Method,
            StatusCode = (int)response.StatusCode,
            response.IsSuccessStatusCode,
            response.ContentType,
            response.BodyLength,
            response.Message,
            response.CorrelationId,
            response.Headers,
            response.CompletionDateTime
        }, new JsonSerializerOptions { WriteIndented = true });

    private void EnsureUnderContentDirectory(string path)
    {
        var root = Path.GetFullPath(_contentDirectory) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        if (!target.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved archive path escaped the run content directory.");
    }

    private static string ResolveWorkspace(string workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace)) return Path.Combine(Directory.GetCurrentDirectory(), "archives");
        if (workspace is "~" or "~/" or "~\\") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (workspace.StartsWith("~/", StringComparison.Ordinal) || workspace.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), workspace[2..]);
        return Path.GetFullPath(workspace);
    }
}
