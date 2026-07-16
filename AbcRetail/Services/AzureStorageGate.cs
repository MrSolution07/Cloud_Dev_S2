using AbcRetail.Options;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Fails closed when AzureStorage connection string is missing.</summary>
public interface IAzureStorageGate
{
    bool IsConfigured { get; }
    string? MissingReason { get; }
}

public sealed class AzureStorageGate : IAzureStorageGate
{
    public AzureStorageGate(IOptions<AzureStorageOptions> options)
    {
        var cs = options.Value.ConnectionString?.Trim();
        IsConfigured = !string.IsNullOrWhiteSpace(cs)
                       && !cs.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
                       && cs.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase);
        MissingReason = IsConfigured
            ? null
            : "Azure Storage connection string is missing. Set AzureStorage:ConnectionString via User Secrets or App Settings (AzureStorage__ConnectionString).";
    }

    public bool IsConfigured { get; }
    public string? MissingReason { get; }
}
