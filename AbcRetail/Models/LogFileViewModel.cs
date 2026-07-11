using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class LogFileViewModel
{
    public string Name { get; set; } = string.Empty;
    public long? Size { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

public class CreateLogViewModel
{
    [Required, StringLength(80)]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Use letters, numbers, dash, underscore, or dot only.")]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Content { get; set; } = string.Empty;
}

public class LogsIndexViewModel
{
    public IReadOnlyList<LogFileViewModel> Files { get; set; } = [];
    public CreateLogViewModel Create { get; set; } = new();
}
