using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AbcRetail.Functions;

/// <summary>
/// HTTP function that writes to and reads from Azure Queue Storage
/// (order-processing and inventory-management).
/// GET peeks messages; POST enqueues a transaction message.
/// </summary>
public sealed class QueueTransactionFunction
{
    private readonly StorageBridge _storage;

    public QueueTransactionFunction(StorageBridge storage)
    {
        _storage = storage;
    }

    [Function("QueueTransaction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "QueueTransaction")] HttpRequestData req,
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

        try
        {
            if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var queue = QueryValue(req.Url, "queue") ?? StorageBridge.OrderQueue;
                var max = int.TryParse(QueryValue(req.Url, "max"), out var n) ? n : 32;
                var messages = await _storage.ReadQueueAsync(queue, max, ct);
                return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, queue = StorageBridge.ResolveQueue(queue), messages });
            }

            var body = await JsonSerializer.DeserializeAsync<QueueWriteRequest>(req.Body, JsonOptions.Default, ct);
            if (body is null || string.IsNullOrWhiteSpace(body.Message))
            {
                return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "message is required." });
            }

            var target = body.Queue ?? StorageBridge.OrderQueue;
            await _storage.WriteQueueAsync(target, body.Message, ct);
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, queue = StorageBridge.ResolveQueue(target) });
        }
        catch (Exception ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }
    }

    private static string? QueryValue(Uri url, string key)
    {
        var query = url.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }
}

public sealed class QueueWriteRequest
{
    public string? Queue { get; set; }
    public string Message { get; set; } = string.Empty;
}
