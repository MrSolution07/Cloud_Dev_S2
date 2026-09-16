using AbcRetail.Options;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Azure Blob Storage for product images / multimedia (container product-images).</summary>
public interface IBlobStorageService
{
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task<(string BlobName, string Url)> UploadProductImageAsync(IFormFile file, CancellationToken ct = default);
    Task<(string BlobName, string Url)> UploadProductImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteBlobAsync(string blobName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListBlobNamesAsync(CancellationToken ct = default);
    string GetBlobUrl(string blobName);
}

public sealed class BlobStorageService : IBlobStorageService
{
    private const long MaxUploadBytes = 1024 * 1024; // 1 MB — keep pages light
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private readonly AzureStorageOptions _options;
    private BlobContainerClient? _container;
    private bool _initialized;

    public BlobStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    private BlobContainerClient Container =>
        _container ??= new BlobContainerClient(_options.ConnectionString, _options.BlobContainer);

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            await Container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        }
        catch (Azure.RequestFailedException)
        {
            await Container.CreateIfNotExistsAsync(cancellationToken: ct);
        }

        _initialized = true;
    }

    public async Task<(string BlobName, string Url)> UploadProductImageAsync(IFormFile file, CancellationToken ct = default)
    {
        await using var input = file.OpenReadStream();
        return await UploadProductImageAsync(input, file.FileName, file.ContentType ?? "application/octet-stream", file.Length, ct);
    }

    public Task<(string BlobName, string Url)> UploadProductImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default) =>
        UploadProductImageAsync(content, fileName, contentType, content.CanSeek ? content.Length : 0, ct);

    private async Task<(string BlobName, string Url)> UploadProductImageAsync(Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        if (length <= 0 && content.CanSeek)
        {
            length = content.Length;
        }

        if (length <= 0)
        {
            throw new InvalidOperationException("Image file is empty.");
        }

        if (length > MaxUploadBytes)
        {
            throw new InvalidOperationException("Image must be 1 MB or smaller for fast page loads.");
        }

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("Only JPEG, PNG, WebP, or GIF images are allowed.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
        }

        var blobName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var blob = Container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        return (blobName, blob.Uri.ToString());
    }

    public async Task DeleteBlobAsync(string blobName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        await EnsureInitializedAsync(ct);
        await Container.GetBlobClient(Path.GetFileName(blobName)).DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<string>> ListBlobNamesAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var names = new List<string>();
        await foreach (var item in Container.GetBlobsAsync(cancellationToken: ct))
        {
            names.Add(item.Name);
        }

        return names;
    }

    public string GetBlobUrl(string blobName) => Container.GetBlobClient(blobName).Uri.ToString();
}
