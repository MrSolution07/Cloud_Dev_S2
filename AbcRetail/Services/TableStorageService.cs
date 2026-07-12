using AbcRetail.Models;
using AbcRetail.Options;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Azure Table Storage for customer profiles and product catalogue rows.</summary>
public interface ITableStorageService
{
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CustomerEntity>> GetCustomersAsync(CancellationToken ct = default);
    Task AddCustomerAsync(CustomerEntity entity, CancellationToken ct = default);
    Task DeleteCustomerAsync(string rowKey, CancellationToken ct = default);
    Task<IReadOnlyList<ProductEntity>> GetProductsAsync(CancellationToken ct = default);
    Task<ProductEntity?> GetProductAsync(string rowKey, CancellationToken ct = default);
    Task AddProductAsync(ProductEntity entity, CancellationToken ct = default);
    Task DeleteProductAsync(string rowKey, CancellationToken ct = default);
    Task UpdateProductStockAsync(string rowKey, int stock, CancellationToken ct = default);
}

public sealed class TableStorageService : ITableStorageService
{
    private readonly AzureStorageOptions _options;
    private TableClient? _customers;
    private TableClient? _products;
    private bool _initialized;

    public TableStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    private TableClient Customers => _customers ??= new TableClient(_options.ConnectionString, _options.CustomersTable);
    private TableClient Products => _products ??= new TableClient(_options.ConnectionString, _options.ProductsTable);

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await Customers.CreateIfNotExistsAsync(ct);
        await Products.CreateIfNotExistsAsync(ct);
        _initialized = true;
    }

    public async Task<IReadOnlyList<CustomerEntity>> GetCustomersAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var results = new List<CustomerEntity>();
        await foreach (var entity in Customers.QueryAsync<CustomerEntity>(e => e.PartitionKey == "CUSTOMER", cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results.OrderByDescending(c => c.Timestamp).ToList();
    }

    public async Task AddCustomerAsync(CustomerEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = "CUSTOMER";
        if (string.IsNullOrWhiteSpace(entity.RowKey))
        {
            entity.RowKey = Guid.NewGuid().ToString("N");
        }

        await Customers.AddEntityAsync(entity, ct);
    }

    public async Task DeleteCustomerAsync(string rowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await Customers.DeleteEntityAsync("CUSTOMER", rowKey, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var results = new List<ProductEntity>();
        await foreach (var entity in Products.QueryAsync<ProductEntity>(e => e.PartitionKey == "PRODUCT", cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results.OrderByDescending(p => p.Timestamp).ToList();
    }

    public async Task<ProductEntity?> GetProductAsync(string rowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        try
        {
            var response = await Products.GetEntityAsync<ProductEntity>("PRODUCT", rowKey, cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddProductAsync(ProductEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = "PRODUCT";
        if (string.IsNullOrWhiteSpace(entity.RowKey))
        {
            entity.RowKey = Guid.NewGuid().ToString("N");
        }

        await Products.AddEntityAsync(entity, ct);
    }

    public async Task DeleteProductAsync(string rowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await Products.DeleteEntityAsync("PRODUCT", rowKey, cancellationToken: ct);
    }

    // Used when an order is dequeued so inventory stays in sync with Table Storage.
    public async Task UpdateProductStockAsync(string rowKey, int stock, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var product = await GetProductAsync(rowKey, ct);
        if (product is null)
        {
            return;
        }

        product.Stock = Math.Max(0, stock);
        await Products.UpdateEntityAsync(product, product.ETag, cancellationToken: ct);
    }
}
