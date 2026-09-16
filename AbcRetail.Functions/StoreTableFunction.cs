using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AbcRetail.Functions;

/// <summary>HTTP function that stores information in Azure Table Storage (Customers, Products, Orders, CartItems).</summary>
public sealed class StoreTableFunction
{
    private readonly StorageBridge _storage;

    public StoreTableFunction(StorageBridge storage)
    {
        _storage = storage;
    }

    [Function("StoreTable")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "StoreTable")] HttpRequestData req,
        CancellationToken ct)
    {
        if (!FunctionAuth.IsAuthorized(req))
        {
            return await FunctionAuth.UnauthorizedAsync(req);
        }

        if (!_storage.IsConfigured)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.ServiceUnavailable, new { error = "Azure Storage connection string is missing." });
        }

        StoreTableRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StoreTableRequest>(req.Body, JsonOptions.Default, ct);
        }
        catch (JsonException ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }

        if (body is null || string.IsNullOrWhiteSpace(body.PartitionKey) || string.IsNullOrWhiteSpace(body.RowKey))
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "table, partitionKey, and rowKey are required." });
        }

        var operation = body.Operation?.Trim() ?? "upsert";
        try
        {
            if (string.Equals(operation, "delete", StringComparison.OrdinalIgnoreCase))
            {
                await _storage.DeleteTableAsync(body.Table, body.PartitionKey, body.RowKey, ct);
            }
            else if (string.Equals(operation, "updateStock", StringComparison.OrdinalIgnoreCase))
            {
                var stock = body.Properties is not null && body.Properties.TryGetValue("Stock", out var stockEl)
                    ? stockEl.GetInt32()
                    : 0;
                await _storage.UpdateStockAsync(body.RowKey, stock, ct);
            }
            else if (string.Equals(operation, "decrementStock", StringComparison.OrdinalIgnoreCase))
            {
                var qty = body.Properties is not null && body.Properties.TryGetValue("Quantity", out var qtyEl)
                    ? qtyEl.GetInt32()
                    : 1;
                await _storage.DecrementStockAsync(body.RowKey, qty, ct);
            }
            else
            {
                await _storage.UpsertTableAsync(body.Table, body.PartitionKey, body.RowKey, body.Properties ?? [], ct);
            }
        }
        catch (Exception ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }

        return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, table = StorageBridge.ResolveTable(body.Table), operation });
    }
}

public sealed class StoreTableRequest
{
    public string Table { get; set; } = string.Empty;
    public string Operation { get; set; } = "upsert";
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Properties { get; set; }
}
