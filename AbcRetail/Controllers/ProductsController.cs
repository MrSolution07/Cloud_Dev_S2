using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
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
        ResolveImages(products);
        return View(products);
    }

    public async Task<IActionResult> Details(string rowKey, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var product = await _tables.GetProductAsync(rowKey, ct);
        if (product is null)
        {
            return NotFound();
        }

        ViewBag.GalleryUrls = product.AllImageBlobNames.Select(_blobs.GetBlobUrl).ToList();
        return View(product);
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var products = await _tables.GetProductsAsync(ct);
        ResolveImages(products);
        ViewBag.BlobCount = (await _blobs.ListBlobNamesAsync(ct)).Count;
        return View(products);
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpGet]
    public IActionResult Create()
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        return View(new ProductCreateViewModel());
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
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

        var uploaded = new List<string>();
        foreach (var image in model.Images.Where(f => f.Length > 0).Take(5))
        {
            try
            {
                var (blobName, _) = await _blobs.UploadProductImageAsync(image, ct);
                uploaded.Add(blobName);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.Images), ex.Message);
                return View(model);
            }
        }

        if (uploaded.Count > 0)
        {
            entity.ImageBlobNames = string.Join('|', uploaded);
            entity.ImageBlobName = uploaded[0];
        }

        await _tables.AddProductAsync(entity, ct);
        await _files.WriteLogAsync(
            $"product-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Created product {entity.Name} images={uploaded.Count} at {DateTime.UtcNow:O}",
            ct);
        TempData["Status"] = "Product saved to Table Storage; images stored in Blob Storage.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
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
        return RedirectToAction(nameof(Manage));
    }

    private void ResolveImages(IEnumerable<ProductEntity> products)
    {
        foreach (var product in products)
        {
            var primary = product.PrimaryImageBlobName;
            if (!string.IsNullOrWhiteSpace(primary))
            {
                product.ImageUrl = _blobs.GetBlobUrl(primary);
            }
        }
    }
}
