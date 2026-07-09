using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class ProductCreateViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [Range(0.01, 1_000_000)]
    public double Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }

    public IFormFile? Image { get; set; }
}
