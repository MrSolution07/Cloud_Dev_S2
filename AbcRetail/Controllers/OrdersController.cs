using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Orders: Azure Table history for customers/admins, plus Phase 1 queue peek/dequeue for admin.</summary>
public class OrdersController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IQueueStorageService _queues;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly IBlobStorageService _blobs;
    private readonly IFunctionGateway _functions;
    private readonly ICartService _cart;

    public OrdersController(
        ITableStorageService tables,
        IQueueStorageService queues,
        IAzureStorageGate gate,
        IFileStorageService files,
        IBlobStorageService blobs,
        IFunctionGateway functions,
        ICartService cart)
    {
        _tables = tables;
        _queues = queues;
        _gate = gate;
        _files = files;
        _blobs = blobs;
        _functions = functions;
        _cart = cart;
    }

    [Authorize]
    public IActionResult Cart() => RedirectToAction("Index", "Cart");

    [Authorize]
    [HttpGet]
    public IActionResult Enqueue() => RedirectToAction("Index", "Products");

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enqueue(string productRowKey, int quantity = 1, CancellationToken ct = default)
    {
        return await AddToCartAsync(productRowKey, quantity, ct);
    }

    private async Task<IActionResult> AddToCartAsync(string productRowKey, int quantity, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            TempData["Error"] = "Admin accounts manage the shop; sign in as a customer to buy.";
            return RedirectToAction("Index", "Products");
        }

        var email = User.Identity?.Name ?? string.Empty;
        var error = await _cart.AddAsync(email, productRowKey, quantity, ct);
        if (error is not null)
        {
            await _files.WriteActivityAsync("CartAddFail", email, error, ct);
            TempData["Error"] = error;
            return RedirectToAction("Index", "Products");
        }

        await _files.WriteActivityAsync("CartAdd", email, $"product={productRowKey} qty={quantity}", ct);
        TempData["Status"] = "Added to cart.";
        return RedirectToAction("Index", "Cart");
    }

    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return RedirectToAction(nameof(Index));
        }

        var email = User.Identity?.Name ?? string.Empty;
        var orders = await _tables.GetOrdersByEmailAsync(email, ct);
        return View(orders);
    }

    [Authorize]
    public async Task<IActionResult> Details(string orderId, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var order = await _tables.GetOrderAsync(orderId, ct);
        if (order is null)
        {
            return NotFound();
        }

        var email = User.Identity?.Name ?? string.Empty;
        var isAdmin = User.IsInRole(CustomerEntity.RoleAdmin);
        if (!isAdmin && !string.Equals(order.CustomerEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        ViewBag.BlobUrlResolver = (Func<string, string>)_blobs.GetBlobUrl;
        return View(new OrderDetailsViewModel
        {
            Order = order,
            Lines = order.GetLines(),
            IsAdmin = isAdmin
        });
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    public async Task<IActionResult> Index(string? status, string? q, CancellationToken ct = default)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var orders = await _tables.GetAllOrdersAsync(ct);
        if (!string.IsNullOrWhiteSpace(status))
        {
            orders = orders.Where(o => string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            orders = orders.Where(o =>
                o.RowKey.Contains(q, StringComparison.OrdinalIgnoreCase)
                || o.CustomerEmail.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        IReadOnlyList<OrderMessageViewModel> peekedOrders;
        IReadOnlyList<OrderMessageViewModel> peekedInventory;
        try
        {
            peekedOrders = await _functions.ReadQueueAsync("order-processing", 32, ct);
            peekedInventory = await _functions.ReadQueueAsync("inventory-management", 32, ct);
        }
        catch
        {
            peekedOrders = await _queues.PeekOrderMessagesAsync(ct: ct);
            peekedInventory = await _queues.PeekInventoryMessagesAsync(ct: ct);
        }

        var model = new OrdersIndexViewModel
        {
            Products = await _tables.GetProductsAsync(ct),
            PeekedOrderMessages = peekedOrders,
            PeekedInventoryMessages = peekedInventory,
            ApproximateOrderCount = await _queues.GetApproximateOrderCountAsync(ct),
            ApproximateInventoryCount = await _queues.GetApproximateInventoryCountAsync(ct),
            TableOrders = orders,
            StatusFilter = status,
            Query = q
        };
        ViewBag.BlobUrlResolver = (Func<string, string>)_blobs.GetBlobUrl;
        return View(model);
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(string orderId, string status, CancellationToken ct)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            OrderEntity.StatusPending, OrderEntity.StatusPaid, OrderEntity.StatusProcessing,
            OrderEntity.StatusShipped, OrderEntity.StatusCompleted, OrderEntity.StatusCancelled, OrderEntity.StatusFailed
        };
        if (!allowed.Contains(status))
        {
            TempData["Error"] = "Invalid order status.";
            return RedirectToAction(nameof(Index));
        }

        var order = await _tables.GetOrderAsync(orderId, ct);
        if (order is null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction(nameof(Index));
        }

        order.Status = status;
        await _functions.UpsertAsync("Orders", order.PartitionKey, order.RowKey, CommerceFormatting.OrderProperties(order), ct);
        await _files.WriteActivityAsync("OrderStatus", User.Identity?.Name, $"order={orderId} status={status}", ct);
        TempData["Status"] = $"Order {orderId[..Math.Min(8, orderId.Length)]}… marked {status}.";
        return RedirectToAction(nameof(Details), new { orderId });
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dequeue(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var message = await _queues.DequeueOneAsync(ct);
        if (message is null)
        {
            TempData["Status"] = "Order queue empty — nothing to dequeue.";
            return RedirectToAction(nameof(Index));
        }

        // Processing order|{productId}|{productName}|{imageName}|{customerEmail}|{utc}|{qty}|{orderId}
        var parts = message.MessageText.Split('|');
        var productId = parts.Length > 1 ? parts[1] : string.Empty;
        var productName = parts.Length > 2 ? parts[2] : "unknown";
        var imageName = parts.Length > 3 ? parts[3] : "none";
        var qty = parts.Length > 6 && int.TryParse(parts[6], out var parsedQty) ? Math.Max(1, parsedQty) : 1;
        var orderId = parts.Length > 7 ? parts[7] : string.Empty;

        var alreadyDeducted = false;
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var order = await _tables.GetOrderAsync(orderId, ct);
            alreadyDeducted = order?.StockDeducted == true;
            if (order is not null && order.Status == OrderEntity.StatusPaid)
            {
                order.Status = OrderEntity.StatusProcessing;
                await _functions.UpsertAsync("Orders", order.PartitionKey, order.RowKey, CommerceFormatting.OrderProperties(order), ct);
            }
        }

        var product = string.IsNullOrWhiteSpace(productId) ? null : await _tables.GetProductAsync(productId, ct);
        var newStock = product?.Stock ?? 0;
        if (!alreadyDeducted && product is not null)
        {
            await _functions.DecrementStockAsync(product.RowKey, qty, ct);
            product = await _tables.GetProductAsync(productId, ct);
            newStock = product?.Stock ?? 0;
        }

        await _functions.WriteQueueAsync(
            "inventory-management",
            CommerceFormatting.InventoryQueueMessage(productId, product?.Name ?? productName, newStock, product?.PrimaryImageBlobName ?? (imageName == "none" ? null : imageName), string.IsNullOrWhiteSpace(orderId) ? "none" : orderId),
            ct);

        await _files.WriteActivityAsync(
            "OrderProcess",
            User.Identity?.Name,
            $"product={productId} name={productName} stock={newStock} qty={qty} image={imageName}",
            ct);

        TempData["Status"] = "Oldest order processed.";
        return RedirectToAction(nameof(Index));
    }
}
