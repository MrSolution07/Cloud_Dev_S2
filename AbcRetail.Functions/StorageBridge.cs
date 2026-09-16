using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;

namespace AbcRetail.Functions;

/// <summary>
/// Calls the same Azure Storage services as the ABC Retail MVC app
/// (Tables, Blobs, Queues, Files) using the same resource names.
/// </summary>
public sealed class StorageBridge
{
    public const string CustomersTable = "Customers";
    public const string ProductsTable = "Products";
    public const string OrdersTable = "Orders";
    public const string CartTable = "CartItems";
    public const string BlobContainer = "product-images";
    public const string OrderQueue = "order-processing";
    public const string InventoryQueue = "inventory-management";
    public const string FileShare = "applogs";
    public const string FileDirectory = "logs";

    private readonly string _connectionString;

    public StorageBridge()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("AzureStorage__ConnectionString")
            ?? Environment.GetEnvironmentVariable("AzureStorage:ConnectionString")
            ?? string.Empty;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_connectionString)
        && !_connectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        && _connectionString.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase);

    public async Task UpsertTableAsync(string table, string partitionKey, string rowKey, Dictionary<string, JsonElement> properties, CancellationToken ct)
    {
        var client = await TableAsync(table, ct);
        var entity = new TableEntity(partitionKey, rowKey);
        foreach (var (key, value) in properties)
        {
            if (key is nameof(TableEntity.PartitionKey) or nameof(TableEntity.RowKey) or nameof(TableEntity.Timestamp) or nameof(TableEntity.ETag) or "odata.etag")
            {
                continue;
            }

            entity[key] = ConvertValue(key, value);
        }

        await client.UpsertEntityAsync(entity, TableUpdateMode.Merge, ct);
    }

    public async Task DeleteTableAsync(string table, string partitionKey, string rowKey, CancellationToken ct)
    {
        var client = await TableAsync(table, ct);
        await client.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: ct);
    }

    public async Task UpdateStockAsync(string productRowKey, int stock, CancellationToken ct)
    {
        var client = await TableAsync(ProductsTable, ct);
        await MutateProductAsync(client, productRowKey, entity =>
        {
            entity["Stock"] = Math.Max(0, stock);
        }, ct);
    }

    public async Task DecrementStockAsync(string productRowKey, int quantity, CancellationToken ct)
    {
        var client = await TableAsync(ProductsTable, ct);
        await MutateProductAsync(client, productRowKey, entity =>
        {
            var current = entity.TryGetValue("Stock", out var raw) && raw is int i ? i : Convert.ToInt32(raw ?? 0);
            entity["Stock"] = Math.Max(0, current - Math.Max(0, quantity));
        }, ct);
    }

    public async Task<(string BlobName, string Url)> UploadBlobAsync(string fileName, string contentType, byte[] bytes, CancellationToken ct)
    {
        var container = new BlobContainerClient(_connectionString, BlobContainer);
        try
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        }
        catch (RequestFailedException)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: ct);
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
        {
            extension = ".bin";
        }

        var blobName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var blob = container.GetBlobClient(blobName);
        await using var stream = new MemoryStream(bytes);
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        return (blobName, blob.Uri.ToString());
    }

    public async Task DeleteBlobAsync(string blobName, CancellationToken ct)
    {
        var container = new BlobContainerClient(_connectionString, BlobContainer);
        await container.GetBlobClient(Path.GetFileName(blobName)).DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task WriteQueueAsync(string queue, string message, CancellationToken ct)
    {
        var client = Queue(queue);
        await client.CreateIfNotExistsAsync(cancellationToken: ct);
        await client.SendMessageAsync(message, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<QueuePeekDto>> ReadQueueAsync(string queue, int max, CancellationToken ct)
    {
        var client = Queue(queue);
        await client.CreateIfNotExistsAsync(cancellationToken: ct);
        var take = Math.Clamp(max, 1, 32);
        var response = await client.PeekMessagesAsync(take, ct);
        return response.Value.Select(m => new QueuePeekDto
        {
            MessageId = m.MessageId,
            MessageText = m.MessageText,
            InsertedOn = m.InsertedOn,
            ExpiresOn = m.ExpiresOn
        }).ToList();
    }

    public async Task WriteFileAsync(string fileName, string content, CancellationToken ct)
    {
        var share = new ShareClient(_connectionString, FileShare);
        await share.CreateIfNotExistsAsync(cancellationToken: ct);
        var dir = share.GetDirectoryClient(FileDirectory);
        await dir.CreateIfNotExistsAsync(cancellationToken: ct);

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new InvalidOperationException("Invalid file name.");
        }

        if (!safeName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".log";
        }

        var file = dir.GetFileClient(safeName);
        var bytes = Encoding.UTF8.GetBytes(content);
        if (await file.ExistsAsync(ct))
        {
            await file.DeleteAsync(cancellationToken: ct);
        }

        await file.CreateAsync(bytes.Length, cancellationToken: ct);
        using var stream = new MemoryStream(bytes);
        await file.UploadRangeAsync(new HttpRange(0, bytes.Length), stream, cancellationToken: ct);
    }

    public static string ResolveQueue(string? name)
    {
        if (string.Equals(name, InventoryQueue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "inventory", StringComparison.OrdinalIgnoreCase))
        {
            return InventoryQueue;
        }

        return OrderQueue;
    }

    public static string ResolveTable(string? name)
    {
        return name?.Trim() switch
        {
            var n when string.Equals(n, ProductsTable, StringComparison.OrdinalIgnoreCase) => ProductsTable,
            var n when string.Equals(n, OrdersTable, StringComparison.OrdinalIgnoreCase) => OrdersTable,
            var n when string.Equals(n, CartTable, StringComparison.OrdinalIgnoreCase) => CartTable,
            _ => CustomersTable
        };
    }

    private async Task<TableClient> TableAsync(string table, CancellationToken ct)
    {
        var name = ResolveTable(table);
        var client = new TableClient(_connectionString, name);
        await client.CreateIfNotExistsAsync(ct);
        return client;
    }

    private QueueClient Queue(string queue) =>
        new(_connectionString, ResolveQueue(queue), new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

    private static async Task MutateProductAsync(TableClient client, string productRowKey, Action<TableEntity> mutate, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var response = await client.GetEntityAsync<TableEntity>("PRODUCT", productRowKey, cancellationToken: ct);
                var entity = response.Value;
                mutate(entity);
                await client.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 412 && attempt < 4)
            {
                await Task.Delay(40 * (attempt + 1), ct);
            }
        }
    }

    private static object? ConvertValue(string key, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (key is "Stock" or "Quantity" or "Qty")
            {
                return value.TryGetInt32(out var i) ? i : (int)value.GetDouble();
            }

            if (value.TryGetInt64(out var l) && value.GetDouble() == l && key is not "Price" and not "Subtotal" and not "Total" and not "UnitPrice")
            {
                return l > int.MaxValue ? l : (int)l;
            }

            return value.GetDouble();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString();
            if (key.EndsWith("Utc", StringComparison.Ordinal) && DateTimeOffset.TryParse(s, out var dto))
            {
                return dto;
            }

            return s;
        }

        return value.ToString();
    }
}

public sealed class QueuePeekDto
{
    public string MessageId { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTimeOffset? InsertedOn { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
}
