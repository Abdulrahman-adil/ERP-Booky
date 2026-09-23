using System.Security.Cryptography;
using Erp.Application.Documents;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Documents;

public sealed class LocalAttachmentStorage(IOptions<AttachmentStorageOptions> options) : IAttachmentStorage
{
    private readonly string storageRoot = Path.GetFullPath(options.Value.StoragePath);

    public async Task<StoredAttachment> StoreAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Directory.CreateDirectory(storageRoot);

        var storageKey = $"{DateTime.UtcNow:yyyy/MM}/{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var targetPath = GetPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(destination, cancellationToken);
        return new StoredAttachment(storageKey, contentType, destination.Length);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storageKey))
        {
            throw new InvalidOperationException("The attachment storage key is invalid.");
        }

        var path = Path.GetFullPath(Path.Combine(storageRoot, storageKey));
        if (!path.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The attachment storage key is outside the configured storage root.");
        }

        return path;
    }
}
