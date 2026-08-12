using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace AbcRetail.Models;

public class ProductEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "PRODUCT";
    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(0.01, 1_000_000)]
    public double Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }

    // Legacy single-image field (kept for backward compatibility with existing table rows).
    public string? ImageBlobName { get; set; }

    // Pipe-separated blob names for multi-image galleries; first entry is the primary/cover image.
    public string? ImageBlobNames { get; set; }

    public string? ImageUrl { get; set; }

    // Not persisted directly: IgnoreDataMember keeps Azure.Data.Tables from trying to serialize
    // this unsupported list type as a table column.
    [IgnoreDataMember]
    public IReadOnlyList<string> AllImageBlobNames
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ImageBlobNames))
            {
                return ImageBlobNames.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            return string.IsNullOrWhiteSpace(ImageBlobName) ? [] : [ImageBlobName];
        }
    }

    [IgnoreDataMember]
    public string? PrimaryImageBlobName => AllImageBlobNames.FirstOrDefault();
}
