namespace AbcRetail.Options;

/// <summary>Names and connection for the four Azure Storage services used by ABC Retail.</summary>
public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string CustomersTable { get; set; } = "Customers";
    public string ProductsTable { get; set; } = "Products";
    public string BlobContainer { get; set; } = "product-images";
    public string QueueName { get; set; } = "order-processing";
    public string InventoryQueueName { get; set; } = "inventory-management";
    public string FileShare { get; set; } = "applogs";
}
