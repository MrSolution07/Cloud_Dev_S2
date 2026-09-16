using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class ProductCreateViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(0.01, 1_000_000)]
    public double Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }

    [Required(ErrorMessage = "Select a category.")]
    [StringLength(80)]
    public string? Category { get; set; }

    // Up to 5 images; first uploaded becomes the cover image.
    public List<IFormFile> Images { get; set; } = [];
}
