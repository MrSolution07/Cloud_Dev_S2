using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AbcRetail.Functions;

/// <summary>HTTP function that writes a file to Azure Files (share applogs / logs).</summary>
public sealed class WriteFileFunction
{
    private readonly StorageBridge _storage;

    public WriteFileFunction(StorageBridge storage)
    {
        _storage = storage;
    }

    [Function("WriteFile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "WriteFile")] HttpRequestData req,
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

        WriteFileRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<WriteFileRequest>(req.Body, JsonOptions.Default, ct);
        }
        catch (JsonException ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }

        if (body is null || string.IsNullOrWhiteSpace(body.FileName) || body.Content is null)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "fileName and content are required." });
        }

        try
        {
            await _storage.WriteFileAsync(body.FileName, body.Content, ct);
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, fileName = body.FileName });
        }
        catch (Exception ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }
    }
}

public sealed class WriteFileRequest
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
