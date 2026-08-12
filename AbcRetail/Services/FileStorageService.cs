using System.Text;
using System.Text.RegularExpressions;
using AbcRetail.Models;
using AbcRetail.Options;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Azure Files share for application log files (stored by filename under applogs/logs).</summary>
public interface IFileStorageService
{
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task WriteLogAsync(string fileName, string content, CancellationToken ct = default);
    /// <summary>Fail-safe activity trace. Never throws; no-ops when storage not configured.</summary>
    Task WriteActivityAsync(string action, string? user, string detail, CancellationToken ct = default);
    Task ClearLogsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LogFileViewModel>> ListLogsAsync(CancellationToken ct = default);
    Task<(Stream Content, string ContentType, string FileName)?> DownloadLogAsync(string fileName, CancellationToken ct = default);
}

public sealed class FileStorageService : IFileStorageService
{
    private static readonly Regex SafeActionRegex = new("[^A-Za-z0-9_-]+", RegexOptions.Compiled);
    private readonly AzureStorageOptions _options;
    private readonly string _directoryName = "logs";
    private ShareClient? _share;
    private bool _initialized;

    public FileStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    private bool HasConnection =>
        !string.IsNullOrWhiteSpace(_options.ConnectionString)
        && !_options.ConnectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    private ShareClient Share => _share ??= new ShareClient(_options.ConnectionString, _options.FileShare);

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await Share.CreateIfNotExistsAsync(cancellationToken: ct);
        var dir = Share.GetDirectoryClient(_directoryName);
        await dir.CreateIfNotExistsAsync(cancellationToken: ct);
        _initialized = true;
    }

    public async Task WriteLogAsync(string fileName, string content, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new InvalidOperationException("Invalid file name.");
        }

        if (!safeName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".log";
        }

        var dir = Share.GetDirectoryClient(_directoryName);
        var file = dir.GetFileClient(safeName);
        var bytes = Encoding.UTF8.GetBytes(content);

        // Overwrite if the same filename already exists (Azure Files Create fails on collision).
        if (await file.ExistsAsync(ct))
        {
            await file.DeleteAsync(cancellationToken: ct);
        }

        await file.CreateAsync(bytes.Length, cancellationToken: ct);
        using var stream = new MemoryStream(bytes);
        await file.UploadRangeAsync(new HttpRange(0, bytes.Length), stream, cancellationToken: ct);
    }

    public async Task WriteActivityAsync(string action, string? user, string detail, CancellationToken ct = default)
    {
        if (!HasConnection)
        {
            return;
        }

        try
        {
            var safeAction = SanitizeAction(action);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var shortGuid = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"activity-{stamp}-{safeAction}-{shortGuid}.log";
            var body =
                $"[{DateTime.UtcNow:O}] action={safeAction} user={user ?? "anonymous"} detail={detail}";
            await WriteLogAsync(fileName, body, ct);
        }
        catch
        {
            // Activity logging must never break register/login/cart/admin flows.
        }
    }

    public async Task ClearLogsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var dir = Share.GetDirectoryClient(_directoryName);
        await foreach (ShareFileItem item in dir.GetFilesAndDirectoriesAsync(cancellationToken: ct))
        {
            if (item.IsDirectory)
            {
                continue;
            }

            await dir.GetFileClient(item.Name).DeleteIfExistsAsync(cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<LogFileViewModel>> ListLogsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var dir = Share.GetDirectoryClient(_directoryName);
        var files = new List<LogFileViewModel>();

        await foreach (ShareFileItem item in dir.GetFilesAndDirectoriesAsync(cancellationToken: ct))
        {
            if (item.IsDirectory)
            {
                continue;
            }

            files.Add(new LogFileViewModel
            {
                Name = item.Name,
                Size = item.FileSize,
                LastModified = null
            });
        }

        return files.OrderByDescending(f => f.Name).ToList();
    }

    public async Task<(Stream Content, string ContentType, string FileName)?> DownloadLogAsync(string fileName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var safeName = Path.GetFileName(fileName);
        var file = Share.GetDirectoryClient(_directoryName).GetFileClient(safeName);
        if (!await file.ExistsAsync(ct))
        {
            return null;
        }

        ShareFileDownloadInfo download = await file.DownloadAsync(cancellationToken: ct);
        var ms = new MemoryStream();
        await download.Content.CopyToAsync(ms, ct);
        ms.Position = 0;
        return (ms, "text/plain", safeName);
    }

    private static string SanitizeAction(string action)
    {
        var cleaned = SafeActionRegex.Replace(action ?? string.Empty, string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? "Event" : cleaned;
    }
}
