using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class ProductEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "PRODUCT";
    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [Range(0.01, 1_000_000)]
    public double Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }

    public string? ImageBlobName { get; set; }

    public string? ImageUrl { get; set; }
}
