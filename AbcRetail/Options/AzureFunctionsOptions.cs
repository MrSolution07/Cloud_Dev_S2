namespace AbcRetail.Options;

public class AzureFunctionsOptions
{
    public const string SectionName = "AzureFunctions";

    /// <summary>Base URL including /api, e.g. http://localhost:7071/api or https://fn-abc.azurewebsites.net/api</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
}
