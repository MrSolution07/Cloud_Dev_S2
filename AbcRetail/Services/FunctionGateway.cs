using System.Net.Http.Json;
using System.Text.Json;
using AbcRetail.Models;
using AbcRetail.Options;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>
/// Writes through Azure Functions when AzureFunctions:BaseUrl is set; otherwise uses the Phase 1 Storage SDK.
/// Reads stay on the MVC storage services.
/// </summary>
public interface IFunctionGateway
{
    bool IsRemote { get; }
    Task UpsertAsync(string table, string partitionKey, string rowKey, IDictionary<string, object?> properties, CancellationToken ct = default);
    Task DeleteAsync(string table, string partitionKey, string rowKey, CancellationToken ct = default);
    Task UpdateStockAsync(string productRowKey, int stock, CancellationToken ct = default);
    Task DecrementStockAsync(string productRowKey, int quantity, CancellationToken ct = default);
    Task<string> WriteBlobAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteBlobAsync(string blobName, CancellationToken ct = default);
    Task WriteQueueAsync(string queue, string message, CancellationToken ct = default);
    Task<IReadOnlyList<OrderMessageViewModel>> ReadQueueAsync(string queue, int max = 32, CancellationToken ct = default);
    Task WriteFileAsync(string fileName, string content, CancellationToken ct = default);
}

public sealed class FunctionGateway : IFunctionGateway
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly AzureFunctionsOptions _functions;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;
    private readonly IQueueStorageService _queues;
    private readonly IFileStorageService _files;

    public FunctionGateway(
        IOptions<AzureFunctionsOptions> functions,
        IHttpClientFactory httpFactory,
        ITableStorageService tables,
        IBlobStorageService blobs,
        IQueueStorageService queues,
        IFileStorageService files)
    {
        _functions = functions.Value;
        _httpFactory = httpFactory;
        _tables = tables;
        _blobs = blobs;
        _queues = queues;
        _files = files;
    }

    public bool IsRemote =>
        !string.IsNullOrWhiteSpace(_functions.BaseUrl)
        && !_functions.BaseUrl.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    public Task UpsertAsync(string table, string partitionKey, string rowKey, IDictionary<string, object?> properties, CancellationToken ct = default) =>
        IsRemote
            ? PostStoreTable("upsert", table, partitionKey, rowKey, properties, ct)
            : _tables.UpsertRawAsync(table, partitionKey, rowKey, properties, ct);

    public Task DeleteAsync(string table, string partitionKey, string rowKey, CancellationToken ct = default) =>
        IsRemote
            ? PostStoreTable("delete", table, partitionKey, rowKey, null, ct)
            : _tables.DeleteRawAsync(table, partitionKey, rowKey, ct);

    public Task UpdateStockAsync(string productRowKey, int stock, CancellationToken ct = default) =>
        IsRemote
            ? PostStoreTable("updateStock", "Products", "PRODUCT", productRowKey, new Dictionary<string, object?> { ["Stock"] = stock }, ct)
            : _tables.UpdateProductStockAsync(productRowKey, stock, ct);

    public Task DecrementStockAsync(string productRowKey, int quantity, CancellationToken ct = default) =>
        IsRemote
            ? PostStoreTable("decrementStock", "Products", "PRODUCT", productRowKey, new Dictionary<string, object?> { ["Quantity"] = quantity }, ct)
            : _tables.DecrementProductStockAsync(productRowKey, quantity, ct);

    public async Task<string> WriteBlobAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!IsRemote)
        {
            var (name, _) = await _blobs.UploadProductImageAsync(content, fileName, contentType, ct);
            return name;
        }

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var payload = new
        {
            operation = "upload",
            fileName,
            contentType,
            contentBase64 = Convert.ToBase64String(ms.ToArray())
        };
        using var response = await SendAsync(HttpMethod.Post, "WriteBlob", payload, ct);
        var body = await response.Content.ReadFromJsonAsync<BlobWriteResponse>(Json, ct)
                   ?? throw new InvalidOperationException("WriteBlob returned an empty body.");
        if (string.IsNullOrWhiteSpace(body.BlobName))
        {
            throw new InvalidOperationException("WriteBlob did not return a blob name.");
        }

        return body.BlobName;
    }

    public Task DeleteBlobAsync(string blobName, CancellationToken ct = default)
    {
        if (!IsRemote)
        {
            return _blobs.DeleteBlobAsync(blobName, ct);
        }

        return SendDiscardAsync(HttpMethod.Post, "WriteBlob", new { operation = "delete", blobName }, ct);
    }

    public Task WriteQueueAsync(string queue, string message, CancellationToken ct = default)
    {
        if (!IsRemote)
        {
            return _queues.SendRawAsync(queue, message, ct);
        }

        return SendDiscardAsync(HttpMethod.Post, "QueueTransaction", new { queue, message }, ct);
    }

    public async Task<IReadOnlyList<OrderMessageViewModel>> ReadQueueAsync(string queue, int max = 32, CancellationToken ct = default)
    {
        if (!IsRemote)
        {
            return queue.Contains("inventory", StringComparison.OrdinalIgnoreCase)
                ? await _queues.PeekInventoryMessagesAsync(max, ct)
                : await _queues.PeekOrderMessagesAsync(max, ct);
        }

        var url = $"QueueTransaction?queue={Uri.EscapeDataString(queue)}&max={max}";
        using var response = await SendAsync(HttpMethod.Get, url, null, ct);
        var body = await response.Content.ReadFromJsonAsync<QueueReadResponse>(Json, ct);
        return body?.Messages ?? [];
    }

    public Task WriteFileAsync(string fileName, string content, CancellationToken ct = default)
    {
        if (!IsRemote)
        {
            return _files.WriteLogAsync(fileName, content, ct);
        }

        return SendDiscardAsync(HttpMethod.Post, "WriteFile", new { fileName, content }, ct);
    }

    private async Task PostStoreTable(string operation, string table, string partitionKey, string rowKey, IDictionary<string, object?>? properties, CancellationToken ct)
    {
        await SendDiscardAsync(HttpMethod.Post, "StoreTable", new
        {
            table,
            operation,
            partitionKey,
            rowKey,
            properties
        }, ct);
    }

    private async Task SendDiscardAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        using var response = await SendAsync(method, path, payload, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("AzureFunctions");
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (!string.IsNullOrWhiteSpace(_functions.Key))
        {
            request.Headers.TryAddWithoutValidation("x-functions-key", _functions.Key);
            request.Headers.TryAddWithoutValidation("x-abc-functions-key", _functions.Key);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Azure Function {path} failed ({(int)response.StatusCode}): {TrimError(detail)}");
        }

        return response;
    }

    private static string TrimError(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return "no response body";
        }

        return detail.Length > 400 ? detail[..400] : detail;
    }

    private sealed class BlobWriteResponse
    {
        public string? BlobName { get; set; }
    }

    private sealed class QueueReadResponse
    {
        public List<OrderMessageViewModel> Messages { get; set; } = [];
    }
}
