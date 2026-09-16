namespace AbcRetail.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = "ABC Retail <noreply@abcretail.local>";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseStartTls { get; set; } = true;
}
