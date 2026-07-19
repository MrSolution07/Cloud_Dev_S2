using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Product catalogue UI: Azure Tables (products) + Azure Blob Storage (images).</summary>
public class ProductsController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;

    public ProductsController(
        ITableStorageService tables,
        IBlobStorageService blobs,
        IAzureStorageGate gate,
        IFileStorageService files)
    {
        _tables = tables;
        _blobs = blobs;
        _gate = gate;
        _files = files;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var products = await _tables.GetProductsAsync(ct);
        foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.ImageBlobName)))
        {
            product.ImageUrl = _blobs.GetBlobUrl(product.ImageBlobName!);
        }

        ViewBag.BlobCount = (await _blobs.ListBlobNamesAsync(ct)).Count;
        return View(products);
    }

    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var products = await _tables.GetProductsAsync(ct);
        foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.ImageBlobName)))
        {
            product.ImageUrl = _blobs.GetBlobUrl(product.ImageBlobName!);
        }

        ViewBag.BlobCount = (await _blobs.ListBlobNamesAsync(ct)).Count;
        return View(products);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        return View(new ProductCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Create(ProductCreateViewModel model, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new ProductEntity
        {
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Stock = model.Stock
        };

        if (model.Image is { Length: > 0 })
        {
            try
            {
                var (blobName, url) = await _blobs.UploadProductImageAsync(model.Image, ct);
                entity.ImageBlobName = blobName;
                entity.ImageUrl = url;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.Image), ex.Message);
                return View(model);
            }
        }

        await _tables.AddProductAsync(entity, ct);
        await _files.WriteLogAsync(
            $"product-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Created product {entity.Name} blob={entity.ImageBlobName ?? "none"} at {DateTime.UtcNow:O}",
            ct);
        TempData["Status"] = "Product saved to Table Storage; image stored in Blob Storage when provided.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string rowKey, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        await _tables.DeleteProductAsync(rowKey, ct);
        TempData["Status"] = "Product deleted from Table Storage.";
        return RedirectToAction(nameof(Index));
    }
}
