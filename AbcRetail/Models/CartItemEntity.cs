using Azure;
using Azure.Data.Tables;

namespace AbcRetail.Models;

/// <summary>Persisted cart line in Azure Table Storage (table CartItems).</summary>
public class CartItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int Quantity { get; set; }

    public static string PartitionFor(string email) => CustomerEntity.NormalizeEmail(email);
}
