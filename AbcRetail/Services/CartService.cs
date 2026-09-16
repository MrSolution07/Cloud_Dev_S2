using AbcRetail.Models;

namespace AbcRetail.Services;

public interface ICartService
{
    Task<CartViewModel> GetAsync(string email, CancellationToken ct = default);
    Task<string?> AddAsync(string email, string productRowKey, int quantity, CancellationToken ct = default);
    Task<string?> UpdateQuantityAsync(string email, string productRowKey, int quantity, CancellationToken ct = default);
    Task RemoveAsync(string email, string productRowKey, CancellationToken ct = default);
    Task ClearAsync(string email, CancellationToken ct = default);
    Task MergeAsync(string fromKey, string toKey, CancellationToken ct = default);
}

public sealed class CartService : ICartService
{
    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;
    private readonly IFunctionGateway _functions;
    private readonly IAzureStorageGate _gate;

    public CartService(
        ITableStorageService tables,
        IBlobStorageService blobs,
        IFunctionGateway functions,
        IAzureStorageGate gate)
    {
        _tables = tables;
        _blobs = blobs;
        _functions = functions;
        _gate = gate;
    }

    public async Task<CartViewModel> GetAsync(string email, CancellationToken ct = default)
    {
        var model = new CartViewModel();
        if (!_gate.IsConfigured || string.IsNullOrWhiteSpace(email))
        {
            return model;
        }

        var items = await _tables.GetCartAsync(email, ct);
        foreach (var item in items)
        {
            var product = await _tables.GetProductAsync(item.RowKey, ct);
            if (product is null)
            {
                model.Lines.Add(new CartLineViewModel
                {
                    ProductRowKey = item.RowKey,
                    Name = "Unavailable product",
                    Quantity = item.Quantity,
                    Unavailable = true
                });
                continue;
            }

            var qty = Math.Max(1, item.Quantity);
            model.Lines.Add(new CartLineViewModel
            {
                ProductRowKey = product.RowKey,
                Name = product.Name,
                Quantity = qty,
                Stock = product.Stock,
                UnitPrice = product.Price,
                ImageUrl = string.IsNullOrWhiteSpace(product.PrimaryImageBlobName) ? null : _blobs.GetBlobUrl(product.PrimaryImageBlobName!),
                Unavailable = product.Stock <= 0,
                QtyExceedsStock = product.Stock > 0 && qty > product.Stock
            });
        }

        model.Subtotal = model.Lines.Where(l => !l.Unavailable).Sum(l => l.LineTotal);
        model.Total = model.Subtotal;
        return model;
    }

    public async Task<string?> AddAsync(string email, string productRowKey, int quantity, CancellationToken ct = default)
    {
        var product = await _tables.GetProductAsync(productRowKey, ct);
        if (product is null)
        {
            return "Product not found.";
        }

        if (product.Stock <= 0)
        {
            return $"{product.Name} is out of stock.";
        }

        var qty = Math.Clamp(quantity, 1, product.Stock);
        var existing = (await _tables.GetCartAsync(email, ct)).FirstOrDefault(i => i.RowKey == productRowKey);
        var next = existing is null ? qty : Math.Clamp(existing.Quantity + qty, 1, product.Stock);

        await _functions.UpsertAsync(
            "CartItems",
            CartItemEntity.PartitionFor(email),
            productRowKey,
            new Dictionary<string, object?> { ["Quantity"] = next },
            ct);
        return null;
    }

    public async Task<string?> UpdateQuantityAsync(string email, string productRowKey, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0)
        {
            await RemoveAsync(email, productRowKey, ct);
            return null;
        }

        var product = await _tables.GetProductAsync(productRowKey, ct);
        if (product is null)
        {
            await RemoveAsync(email, productRowKey, ct);
            return "That product is no longer available and was removed from your cart.";
        }

        if (product.Stock <= 0)
        {
            return $"{product.Name} is out of stock.";
        }

        var next = Math.Clamp(quantity, 1, product.Stock);
        await _functions.UpsertAsync(
            "CartItems",
            CartItemEntity.PartitionFor(email),
            productRowKey,
            new Dictionary<string, object?> { ["Quantity"] = next },
            ct);
        return next < quantity ? $"Quantity limited to {product.Stock} in stock." : null;
    }

    public Task RemoveAsync(string email, string productRowKey, CancellationToken ct = default) =>
        _functions.DeleteAsync("CartItems", CartItemEntity.PartitionFor(email), productRowKey, ct);

    public async Task ClearAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var items = await _tables.GetCartAsync(email, ct);
        foreach (var item in items)
        {
            await _functions.DeleteAsync("CartItems", item.PartitionKey, item.RowKey, ct);
        }
    }

    public async Task MergeAsync(string fromKey, string toKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey)
            || string.Equals(fromKey, toKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var source = await _tables.GetCartAsync(fromKey, ct);
        foreach (var item in source)
        {
            await AddAsync(toKey, item.RowKey, item.Quantity, ct);
        }

        await ClearAsync(fromKey, ct);
    }
}
