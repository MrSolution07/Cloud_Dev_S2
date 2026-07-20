using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Orders UI backed by Azure Queues (order-processing + inventory-management).</summary>
public class OrdersController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IQueueStorageService _queues;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;

    public OrdersController(
        ITableStorageService tables,
        IQueueStorageService queues,
        IAzureStorageGate gate,
        IFileStorageService files)
    {
        _tables = tables;
        _queues = queues;
        _gate = gate;
        _files = files;
    }

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
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enqueue(string productRowKey, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var product = await _tables.GetProductAsync(productRowKey, ct);
        if (product is null)
        {
            TempData["Error"] = "Product not found.";
            return RedirectToAction(nameof(Index));
        }

        await _queues.SendOrderMessageAsync(product.RowKey, product.Name, product.ImageBlobName, ct);
        await _files.WriteLogAsync(
            $"order-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Queued Processing order for {product.Name} ({product.RowKey}) image={product.ImageBlobName ?? "none"}",
            ct);
        TempData["Status"] = "Order message sent to Azure Queue (order-processing).";
        return RedirectToAction(nameof(Index));
    }

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

        // Processing order|{productId}|{productName}|{imageName}|{utc}
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
            product?.ImageBlobName ?? (imageName == "none" ? null : imageName),
            ct);

        await _files.WriteLogAsync(
            $"inventory-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Processed order → inventory update product={productId} stock={newStock} image={imageName}",
            ct);

        TempData["Status"] = $"Dequeued: {message.MessageText}. Inventory message queued (stock={newStock}).";
        return RedirectToAction(nameof(Index));
    }
}
