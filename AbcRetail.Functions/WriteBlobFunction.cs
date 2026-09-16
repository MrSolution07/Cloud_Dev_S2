using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AbcRetail.Functions;

/// <summary>HTTP function that writes (or deletes) blobs in the product-images container.</summary>
public sealed class WriteBlobFunction
{
    private readonly StorageBridge _storage;

    public WriteBlobFunction(StorageBridge storage)
    {
        _storage = storage;
    }

    [Function("WriteBlob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "WriteBlob")] HttpRequestData req,
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

        WriteBlobRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<WriteBlobRequest>(req.Body, JsonOptions.Default, ct);
        }
        catch (JsonException ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }

        if (body is null)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "JSON body required." });
        }

        try
        {
            if (string.Equals(body.Operation, "delete", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(body.BlobName))
                {
                    return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "blobName is required to delete." });
                }

                await _storage.DeleteBlobAsync(body.BlobName, ct);
                return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, operation = "delete", blobName = body.BlobName });
            }

            if (string.IsNullOrWhiteSpace(body.ContentBase64))
            {
                return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "contentBase64 is required to upload." });
            }

            var bytes = Convert.FromBase64String(body.ContentBase64);
            if (bytes.Length > 1024 * 1024)
            {
                return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = "Image must be 1 MB or smaller." });
            }

            var (blobName, url) = await _storage.UploadBlobAsync(
                body.FileName ?? "upload.bin",
                string.IsNullOrWhiteSpace(body.ContentType) ? "application/octet-stream" : body.ContentType,
                bytes,
                ct);

            return await FunctionAuth.JsonAsync(req, HttpStatusCode.OK, new { ok = true, blobName, url });
        }
        catch (Exception ex)
        {
            return await FunctionAuth.JsonAsync(req, HttpStatusCode.BadRequest, new { error = ex.Message });
        }
    }
}

public sealed class WriteBlobRequest
{
    public string Operation { get; set; } = "upload";
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? ContentBase64 { get; set; }
    public string? BlobName { get; set; }
}
