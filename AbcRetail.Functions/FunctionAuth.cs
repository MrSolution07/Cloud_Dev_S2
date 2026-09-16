using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace AbcRetail.Functions;

internal static class FunctionAuth
{
    public static bool IsAuthorized(HttpRequestData req)
    {
        var expected = Environment.GetEnvironmentVariable("FUNCTIONS_ACCESS_KEY")
                       ?? Environment.GetEnvironmentVariable("AzureFunctions__Key");

        if (string.IsNullOrWhiteSpace(expected) || expected.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            // Local coursework host (no WEBSITE_INSTANCE_ID) may run without a key.
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        }

        if (req.Headers.TryGetValues("x-functions-key", out var values)
            || req.Headers.TryGetValues("x-abc-functions-key", out values))
        {
            return values.Any(v => string.Equals(v, expected, StringComparison.Ordinal));
        }

        return false;
    }

    public static async Task<HttpResponseData> UnauthorizedAsync(HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.Unauthorized);
        await res.WriteStringAsync("Missing or invalid function key.");
        return res;
    }

    public static async Task<HttpResponseData> JsonAsync(HttpRequestData req, HttpStatusCode status, object body)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(body);
        return res;
    }
}
