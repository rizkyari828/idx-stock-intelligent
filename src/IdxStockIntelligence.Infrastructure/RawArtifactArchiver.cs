using System.Security.Cryptography;

namespace IdxStockIntelligence.Infrastructure;

public sealed record ArchivedArtifact(
    Guid ArtifactId,
    string RelativePath,
    string ContentSha256,
    long ByteLength,
    DateTimeOffset ArchivedAt);

public sealed class RawArtifactArchiver
{
    private readonly string _root;

    public RawArtifactArchiver(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Artifact root is required.", nameof(root));
        }

        _root = Path.GetFullPath(root);
    }

    public async Task<ArchivedArtifact> ArchiveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException("Artifact stream must be readable.", nameof(content));
        }

        var safeExtension = NormalizeExtension(extension);
        Directory.CreateDirectory(_root);
        var temporaryPath = Path.Combine(_root, $".archive-{Guid.NewGuid():N}.tmp");
        var archivedAt = DateTimeOffset.UtcNow;
        long byteLength = 0;
        string digest;

        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hasher.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    byteLength += read;
                }

                await destination.FlushAsync(cancellationToken);
            }

            digest = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            var relativePath = Path.Combine(digest[..2], $"{digest}{safeExtension}");
            var finalPath = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                File.Delete(temporaryPath);
            }

            return new ArchivedArtifact(Guid.NewGuid(), relativePath, digest, byteLength, archivedAt);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".bin";
        }

        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        if (normalized.Length > 16 || normalized.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("Artifact extension must be short and alphanumeric.", nameof(extension));
        }

        return normalized.ToLowerInvariant();
    }
}
