using System.Text.Json;
using AbcRetail.Models;

namespace AbcRetail.Services;

/// <summary>
/// Upserts local wwwroot/images/catalog items into Azure Tables + Blobs
/// so shop photos (AirPods, Z Fold, Sony, …) actually appear.
/// </summary>
public static class CatalogSeeder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task EnsureAsync(
        IWebHostEnvironment env,
        ITableStorageService tables,
        IBlobStorageService blobs,
        ILogger logger,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(env.WebRootPath, "images", "catalog");
        var specPath = Path.Combine(dir, "descriptions.json");
        if (!File.Exists(specPath))
        {
            return;
        }

        List<CatalogItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<CatalogItem>>(await File.ReadAllTextAsync(specPath, ct), Json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Catalog descriptions.json is invalid.");
            return;
        }

        if (items is null || items.Count == 0)
        {
            return;
        }

        var existing = await tables.GetProductsAsync(ct);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.File))
            {
                continue;
            }

            var match = existing.FirstOrDefault(p =>
                string.Equals(p.Name, item.Name, StringComparison.OrdinalIgnoreCase));
            var imagePath = Path.Combine(dir, item.File);
            if (!File.Exists(imagePath))
            {
                logger.LogWarning("Catalog image missing: {File}", item.File);
                continue;
            }

            if (match is not null)
            {
                if (!string.IsNullOrWhiteSpace(match.PrimaryImageBlobName))
                {
                    continue;
                }

                try
                {
                    await using var stream = File.OpenRead(imagePath);
                    var (blobName, _) = await blobs.UploadProductImageAsync(stream, item.File, "image/jpeg", ct);
                    match.ImageBlobName = blobName;
                    match.ImageBlobNames = blobName;
                    await tables.UpdateProductAsync(match, ct);
                    logger.LogInformation("Attached catalog image to {Name}", match.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to attach catalog image to {Name}", item.Name);
                }

                continue;
            }

            try
            {
                await using var stream = File.OpenRead(imagePath);
                var (blobName, _) = await blobs.UploadProductImageAsync(stream, item.File, "image/jpeg", ct);
                var entity = new ProductEntity
                {
                    Name = item.Name.Trim(),
                    Description = item.Description,
                    Price = item.Price > 0 ? item.Price : 1999,
                    Stock = item.Stock > 0 ? item.Stock : 12,
                    Category = string.IsNullOrWhiteSpace(item.Category) ? "Other" : item.Category.Trim(),
                    ImageBlobName = blobName,
                    ImageBlobNames = blobName
                };
                await tables.AddProductAsync(entity, ct);
                logger.LogInformation("Seeded catalog product {Name}", entity.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed catalog product {Name}", item.Name);
            }
        }
    }

    private sealed class CatalogItem
    {
        public string File { get; set; } = string.Empty;
        public string Category { get; set; } = "Electronics";
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Price { get; set; }
        public int Stock { get; set; }
    }
}
