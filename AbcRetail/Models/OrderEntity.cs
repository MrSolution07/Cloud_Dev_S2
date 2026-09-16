using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace AbcRetail.Models;

public class OrderLineItem
{
    public string ProductRowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public string? ImageBlobName { get; set; }
}

/// <summary>Durable customer order stored in Azure Table Storage (table Orders).</summary>
public class OrderEntity : ITableEntity
{
    public const string StatusPending = "Pending";
    public const string StatusPaid = "Paid";
    public const string StatusProcessing = "Processing";
    public const string StatusShipped = "Shipped";
    public const string StatusCompleted = "Completed";
    public const string StatusCancelled = "Cancelled";
    public const string StatusFailed = "Failed";

    public const string PaymentInitiated = "Initiated";
    public const string PaymentProcessing = "Processing";
    public const string PaymentSucceeded = "Succeeded";
    public const string PaymentFailed = "Failed";
    public const string PaymentCancelled = "Cancelled";

    public const string Partition = "ORDER";

    public string PartitionKey { get; set; } = Partition;
    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string CustomerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = StatusPending;
    public string PaymentStatus { get; set; } = PaymentInitiated;
    public string? PaymentReference { get; set; }
    public string? CardLast4 { get; set; }
    public double Subtotal { get; set; }
    public double Total { get; set; }
    public string ShipFirstName { get; set; } = string.Empty;
    public string ShipLastName { get; set; } = string.Empty;
    public string? ShipPhone { get; set; }
    public string? ShipCity { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string LineItemsJson { get; set; } = "[]";
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool StockDeducted { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<OrderLineItem> GetLines()
    {
        if (string.IsNullOrWhiteSpace(LineItemsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<OrderLineItem>>(LineItemsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void SetLines(IEnumerable<OrderLineItem> lines)
    {
        LineItemsJson = JsonSerializer.Serialize(lines.ToList());
    }
}
