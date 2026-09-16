using AbcRetail.Models;

namespace AbcRetail.Services;

/// <summary>
/// Shop search, category matching, and sort. Token search so "galaxy fold" hits
/// "Samsung Galaxy Z Fold".
/// </summary>
public static class ProductQuery
{
    public static readonly string[] CanonicalCategories =
    [
        "Phones", "Tablets", "Laptops", "Audio", "Cameras",
        "Electronics", "Home", "Outdoor", "Apparel", "Other"
    ];

    private static readonly HashSet<string> ElectronicsBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Electronics", "Phones", "Tablets", "Laptops", "Audio", "Cameras",
        "Apple", "Samsung", "Google", "Sony"
    };

    public static IReadOnlyList<string> FilterOptions(IEnumerable<ProductEntity> products) =>
        products
            .Select(p => p.Category?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<ProductEntity> Apply(
        IEnumerable<ProductEntity> products,
        string? q,
        string? category,
        string? sort = null)
    {
        var list = products.Where(p => MatchesSearch(p, q) && MatchesCategory(p, category)).ToList();
        return sort switch
        {
            "price-asc" => list.OrderBy(p => p.Price).ThenBy(p => p.Name).ToList(),
            "price-desc" => list.OrderByDescending(p => p.Price).ThenBy(p => p.Name).ToList(),
            _ => list.OrderByDescending(p => p.Timestamp).ThenBy(p => p.Name).ToList()
        };
    }

    public static bool MatchesSearch(ProductEntity product, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return true;
        }

        var haystack = Normalize($"{product.Name} {product.Description} {product.Category}");
        foreach (var token in Normalize(q).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesCategory(ProductEntity product, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        var actual = string.IsNullOrWhiteSpace(product.Category) ? "Other" : product.Category.Trim();
        if (actual.Equals(category, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bucket = InferBucket(product);
        if (bucket.Equals(category, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (category.Equals("Electronics", StringComparison.OrdinalIgnoreCase))
        {
            return ElectronicsBuckets.Contains(actual) || ElectronicsBuckets.Contains(bucket);
        }

        return false;
    }

    public static string InferBucket(ProductEntity product)
    {
        var text = Normalize($"{product.Name} {product.Category}");
        if (ContainsAny(text, "ipad", "tablet", "tabs10", "tab s"))
        {
            return "Tablets";
        }

        if (ContainsAny(text, "macbook", "laptop"))
        {
            return "Laptops";
        }

        if (ContainsAny(text, "airpods", "earbuds", "headphone", "speaker"))
        {
            return "Audio";
        }

        if (ContainsAny(text, "camera", "mirrorless"))
        {
            return "Cameras";
        }

        if (ContainsAny(text, "iphone", "pixel", "galaxy", "phone", "fold"))
        {
            return "Phones";
        }

        if (ElectronicsBuckets.Contains(product.Category ?? string.Empty))
        {
            return "Electronics";
        }

        return string.IsNullOrWhiteSpace(product.Category) ? "Other" : product.Category.Trim();
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(Normalize(n), StringComparison.Ordinal));
}
