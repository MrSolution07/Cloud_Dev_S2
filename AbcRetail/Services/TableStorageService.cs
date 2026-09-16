using AbcRetail.Models;
using AbcRetail.Options;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Azure Table Storage for customer/admin login profiles and product catalogue rows.</summary>
public interface ITableStorageService
{
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CustomerEntity>> GetCustomersAsync(CancellationToken ct = default);
    Task<CustomerEntity?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task AddCustomerAsync(CustomerEntity entity, CancellationToken ct = default);
    Task UpdateCustomerAsync(CustomerEntity entity, CancellationToken ct = default);
    Task DeleteCustomerAsync(string rowKey, CancellationToken ct = default);
    Task<IReadOnlyList<ProductEntity>> GetProductsAsync(CancellationToken ct = default);
    Task<ProductEntity?> GetProductAsync(string rowKey, CancellationToken ct = default);
    Task AddProductAsync(ProductEntity entity, CancellationToken ct = default);
    Task UpdateProductAsync(ProductEntity entity, CancellationToken ct = default);
    Task DeleteProductAsync(string rowKey, CancellationToken ct = default);
    Task UpdateProductStockAsync(string rowKey, int stock, CancellationToken ct = default);
    Task DecrementProductStockAsync(string rowKey, int quantity, CancellationToken ct = default);

    Task<IReadOnlyList<CartItemEntity>> GetCartAsync(string email, CancellationToken ct = default);
    Task UpsertCartItemAsync(CartItemEntity entity, CancellationToken ct = default);
    Task DeleteCartItemAsync(string email, string productRowKey, CancellationToken ct = default);
    Task ClearCartAsync(string email, CancellationToken ct = default);

    Task AddOrderAsync(OrderEntity entity, CancellationToken ct = default);
    Task UpdateOrderAsync(OrderEntity entity, CancellationToken ct = default);
    Task<OrderEntity?> GetOrderAsync(string orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderEntity>> GetOrdersByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<OrderEntity>> GetAllOrdersAsync(CancellationToken ct = default);
    Task<OrderEntity?> FindOrderByIdempotencyAsync(string email, string idempotencyKey, CancellationToken ct = default);

    Task UpsertRawAsync(string table, string partitionKey, string rowKey, IDictionary<string, object?> properties, CancellationToken ct = default);
    Task DeleteRawAsync(string table, string partitionKey, string rowKey, CancellationToken ct = default);
}

public sealed class TableStorageService : ITableStorageService
{
    private readonly AzureStorageOptions _options;
    private TableClient? _customers;
    private TableClient? _products;
    private TableClient? _orders;
    private TableClient? _cart;
    private bool _initialized;

    public TableStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    private TableClient Customers => _customers ??= new TableClient(_options.ConnectionString, _options.CustomersTable);
    private TableClient Products => _products ??= new TableClient(_options.ConnectionString, _options.ProductsTable);
    private TableClient Orders => _orders ??= new TableClient(_options.ConnectionString, _options.OrdersTable);
    private TableClient Cart => _cart ??= new TableClient(_options.ConnectionString, _options.CartTable);

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await Customers.CreateIfNotExistsAsync(ct);
        await Products.CreateIfNotExistsAsync(ct);
        await Orders.CreateIfNotExistsAsync(ct);
        await Cart.CreateIfNotExistsAsync(ct);
        _initialized = true;
    }

    // Full table scan (partition USER for logins + legacy CUSTOMER rows created before auth existed).
    public async Task<IReadOnlyList<CustomerEntity>> GetCustomersAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var results = new List<CustomerEntity>();
        await foreach (var entity in Customers.QueryAsync<CustomerEntity>(cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results.OrderByDescending(c => c.Timestamp).ToList();
    }

    public async Task<CustomerEntity?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        try
        {
            var response = await Customers.GetEntityAsync<CustomerEntity>("USER", CustomerEntity.NormalizeEmail(email), cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Registration: row key is the normalized email so login can look it up directly.
    public async Task AddCustomerAsync(CustomerEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = "USER";
        entity.RowKey = CustomerEntity.NormalizeEmail(entity.Email);
        await Customers.AddEntityAsync(entity, ct);
    }

    public async Task UpdateCustomerAsync(CustomerEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await Customers.UpdateEntityAsync(entity, entity.ETag, cancellationToken: ct);
    }

    public async Task DeleteCustomerAsync(string rowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        try
        {
            await Customers.DeleteEntityAsync("USER", rowKey, cancellationToken: ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // legacy row created before auth existed
            await Customers.DeleteEntityAsync("CUSTOMER", rowKey, cancellationToken: ct);
        }
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

    public async Task UpdateProductAsync(ProductEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = "PRODUCT";
        await Products.UpdateEntityAsync(entity, entity.ETag, cancellationToken: ct);
    }

    // Used when an order is dequeued so inventory stays in sync with Table Storage.
    public async Task UpdateProductStockAsync(string rowKey, int stock, CancellationToken ct = default)
    {
        await MutateProductAsync(rowKey, p => p.Stock = Math.Max(0, stock), ct);
    }

    public async Task DecrementProductStockAsync(string rowKey, int quantity, CancellationToken ct = default)
    {
        await MutateProductAsync(rowKey, p => p.Stock = Math.Max(0, p.Stock - Math.Max(0, quantity)), ct);
    }

    public async Task<IReadOnlyList<CartItemEntity>> GetCartAsync(string email, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var pk = CartItemEntity.PartitionFor(email);
        var results = new List<CartItemEntity>();
        await foreach (var entity in Cart.QueryAsync<CartItemEntity>(e => e.PartitionKey == pk, cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results;
    }

    public async Task UpsertCartItemAsync(CartItemEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await Cart.UpsertEntityAsync(entity, Azure.Data.Tables.TableUpdateMode.Replace, ct);
    }

    public async Task DeleteCartItemAsync(string email, string productRowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await Cart.DeleteEntityAsync(CartItemEntity.PartitionFor(email), productRowKey, cancellationToken: ct);
    }

    public async Task ClearCartAsync(string email, CancellationToken ct = default)
    {
        var items = await GetCartAsync(email, ct);
        foreach (var item in items)
        {
            await Cart.DeleteEntityAsync(item.PartitionKey, item.RowKey, cancellationToken: ct);
        }
    }

    public async Task AddOrderAsync(OrderEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = OrderEntity.Partition;
        if (string.IsNullOrWhiteSpace(entity.RowKey))
        {
            entity.RowKey = Guid.NewGuid().ToString("N");
        }

        await Orders.AddEntityAsync(entity, ct);
    }

    public async Task UpdateOrderAsync(OrderEntity entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        entity.PartitionKey = OrderEntity.Partition;
        await Orders.UpdateEntityAsync(entity, entity.ETag, cancellationToken: ct);
    }

    public async Task<OrderEntity?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        try
        {
            var response = await Orders.GetEntityAsync<OrderEntity>(OrderEntity.Partition, orderId, cancellationToken: ct);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<OrderEntity>> GetOrdersByEmailAsync(string email, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var normalized = CustomerEntity.NormalizeEmail(email);
        var results = new List<OrderEntity>();
        await foreach (var entity in Orders.QueryAsync<OrderEntity>(e => e.PartitionKey == OrderEntity.Partition, cancellationToken: ct))
        {
            if (string.Equals(entity.CustomerEmail, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entity.CustomerEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(entity);
            }
        }

        return results.OrderByDescending(o => o.CreatedUtc).ToList();
    }

    public async Task<IReadOnlyList<OrderEntity>> GetAllOrdersAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var results = new List<OrderEntity>();
        await foreach (var entity in Orders.QueryAsync<OrderEntity>(e => e.PartitionKey == OrderEntity.Partition, cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results.OrderByDescending(o => o.CreatedUtc).ToList();
    }

    public async Task<OrderEntity?> FindOrderByIdempotencyAsync(string email, string idempotencyKey, CancellationToken ct = default)
    {
        var orders = await GetOrdersByEmailAsync(email, ct);
        return orders.FirstOrDefault(o =>
            string.Equals(o.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)
            && o.Status is OrderEntity.StatusPending or OrderEntity.StatusFailed);
    }

    public async Task UpsertRawAsync(string table, string partitionKey, string rowKey, IDictionary<string, object?> properties, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var client = ResolveTable(table);
        var entity = new Azure.Data.Tables.TableEntity(partitionKey, rowKey);
        foreach (var (key, value) in properties)
        {
            if (key is "PartitionKey" or "RowKey" or "Timestamp" or "ETag" or "odata.etag" || value is null)
            {
                continue;
            }

            entity[key] = value;
        }

        await client.UpsertEntityAsync(entity, Azure.Data.Tables.TableUpdateMode.Merge, ct);
    }

    public async Task DeleteRawAsync(string table, string partitionKey, string rowKey, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var client = ResolveTable(table);
        try
        {
            await client.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
        }
    }

    private TableClient ResolveTable(string table) =>
        table.Trim() switch
        {
            var n when string.Equals(n, _options.ProductsTable, StringComparison.OrdinalIgnoreCase) || string.Equals(n, "Products", StringComparison.OrdinalIgnoreCase) => Products,
            var n when string.Equals(n, _options.OrdersTable, StringComparison.OrdinalIgnoreCase) || string.Equals(n, "Orders", StringComparison.OrdinalIgnoreCase) => Orders,
            var n when string.Equals(n, _options.CartTable, StringComparison.OrdinalIgnoreCase) || string.Equals(n, "CartItems", StringComparison.OrdinalIgnoreCase) => Cart,
            _ => Customers
        };

    private async Task MutateProductAsync(string rowKey, Action<ProductEntity> mutate, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var product = await GetProductAsync(rowKey, ct);
            if (product is null)
            {
                return;
            }

            mutate(product);
            try
            {
                await Products.UpdateEntityAsync(product, product.ETag, cancellationToken: ct);
                return;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 412 && attempt < 4)
            {
                await Task.Delay(40 * (attempt + 1), ct);
            }
        }
    }
}
