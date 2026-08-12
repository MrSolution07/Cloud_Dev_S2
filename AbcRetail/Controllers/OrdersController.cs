using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Orders backed by Azure Queues (order-processing + inventory-management).
/// Customers only see/act on their own orders (Cart); Admin sees and processes everything (Index).</summary>
public class OrdersController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IQueueStorageService _queues;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly IBlobStorageService _blobs;

    public OrdersController(
        ITableStorageService tables,
        IQueueStorageService queues,
        IAzureStorageGate gate,
        IFileStorageService files,
        IBlobStorageService blobs)
    {
        _tables = tables;
        _queues = queues;
        _gate = gate;
        _files = files;
        _blobs = blobs;
    }

    // Admin: full order + inventory queues, with the raw message text and thumbnails.
    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var model = new OrdersIndexViewModel
        {
            Products = await _tables.GetProductsAsync(ct),
            PeekedOrderMessages = await _queues.PeekOrderMessagesAsync(ct: ct),
            PeekedInventoryMessages = await _queues.PeekInventoryMessagesAsync(ct: ct),
            ApproximateOrderCount = await _queues.GetApproximateOrderCountAsync(ct),
            ApproximateInventoryCount = await _queues.GetApproximateInventoryCountAsync(ct)
        };
        ViewBag.BlobUrlResolver = (Func<string, string>)_blobs.GetBlobUrl;
        return View(model);
    }

    // Customer: only their own placed orders (matched by email in the queue message).
    [Authorize]
    public async Task<IActionResult> Cart(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var email = User.Identity?.Name ?? string.Empty;
        var all = await _queues.PeekOrderMessagesAsync(ct: ct);
        var mine = all.Where(m =>
        {
            var parts = m.MessageText.Split('|');
            return parts.Length > 4 && string.Equals(parts[4], email, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        ViewBag.BlobUrlResolver = (Func<string, string>)_blobs.GetBlobUrl;
        return View(mine);
    }

    [Authorize]
    [HttpGet]
    // Safety net: cookie auth ReturnUrl often points here after login, but Enqueue is POST-only.
    // GET would otherwise 404 — send shoppers back to the catalog instead.
    public IActionResult Enqueue() => RedirectToAction("Index", "Products");

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enqueue(string productRowKey, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var product = await _tables.GetProductAsync(productRowKey, ct);
        var email = User.Identity?.Name ?? "unknown";
        if (product is null)
        {
            await _files.WriteActivityAsync("CartAddFail", email, $"productRowKey={productRowKey} reason=not_found", ct);
            TempData["Error"] = "Product not found.";
            return RedirectToAction("Index", "Products");
        }

        await _queues.SendOrderMessageAsync(product.RowKey, product.Name, product.PrimaryImageBlobName, email, ct);
        await _files.WriteActivityAsync(
            "CartAdd",
            email,
            $"product={product.Name} id={product.RowKey} image={product.PrimaryImageBlobName ?? "none"}",
            ct);
        TempData["Status"] = "Order message sent!(order-processing).";
        return RedirectToAction(nameof(Cart));
    }

    // Admin only: process oldest order, decrement stock, queue an inventory-management message.
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

        // Processing order|{productId}|{productName}|{imageName}|{customerEmail}|{utc}
        var parts = message.MessageText.Split('|');
        var productId = parts.Length > 1 ? parts[1] : string.Empty;
        var productName = parts.Length > 2 ? parts[2] : "unknown";
        var imageName = parts.Length > 3 ? parts[3] : "none";

        var product = string.IsNullOrWhiteSpace(productId) ? null : await _tables.GetProductAsync(productId, ct);
        var newStock = product is null ? 0 : Math.Max(0, product.Stock - 1);
        if (product is not null)
        {
            await _tables.UpdateProductStockAsync(product.RowKey, newStock, ct);
        }

        await _queues.SendInventoryMessageAsync(
            productId,
            product?.Name ?? productName,
            newStock,
            product?.PrimaryImageBlobName ?? (imageName == "none" ? null : imageName),
            ct);

        await _files.WriteActivityAsync(
            "OrderProcess",
            User.Identity?.Name,
            $"product={productId} name={productName} stock={newStock} image={imageName}",
            ct);

        TempData["Status"] = $"Dequeued: {message.MessageText}. Inventory message queued (stock={newStock}).";
        return RedirectToAction(nameof(Index));
    }
}
