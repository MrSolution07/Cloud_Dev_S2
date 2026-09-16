using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Product catalogue UI: Azure Tables (products) + Azure Blob Storage (images) via Functions when configured.</summary>
public class ProductsController : Controller
{
    public static readonly string[] Categories = ProductQuery.CanonicalCategories;

    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly IFunctionGateway _functions;

    public ProductsController(
        ITableStorageService tables,
        IBlobStorageService blobs,
        IAzureStorageGate gate,
        IFileStorageService files,
        IFunctionGateway functions)
    {
        _tables = tables;
        _blobs = blobs;
        _gate = gate;
        _files = files;
        _functions = functions;
    }

    public async Task<IActionResult> Index(string? q, string? category, string? sort, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var all = await _tables.GetProductsAsync(ct);
        var list = ProductQuery.Apply(all, q, category, sort);
        ResolveImages(list);
        ViewBag.Query = q;
        ViewBag.Category = category;
        ViewBag.Sort = sort;
        ViewBag.Categories = ProductQuery.FilterOptions(all);
        ViewBag.TotalCount = all.Count;
        return View(list);
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
    public async Task<IActionResult> Manage(string? q, string? category, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var all = await _tables.GetProductsAsync(ct);
        var products = ProductQuery.Apply(all, q, category);
        ResolveImages(products);
        ViewBag.BlobCount = (await _blobs.ListBlobNamesAsync(ct)).Count;
        ViewBag.Query = q;
        ViewBag.Category = category;
        ViewBag.Categories = ProductQuery.FilterOptions(all);
        ViewBag.TotalCount = all.Count;
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

        ViewBag.Categories = Categories;
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
            ViewBag.Categories = Categories;
            return View(model);
        }

        var entity = new ProductEntity
        {
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Stock = model.Stock,
            Category = model.Category!.Trim()
        };

        var uploaded = await UploadImagesAsync(model.Images, ct);
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = Categories;
            return View(model);
        }

        if (uploaded.Count > 0)
        {
            entity.ImageBlobNames = string.Join('|', uploaded);
            entity.ImageBlobName = uploaded[0];
        }

        await _functions.UpsertAsync("Products", entity.PartitionKey, entity.RowKey, CommerceFormatting.ProductProperties(entity), ct);
        await _files.WriteActivityAsync(
            "ProductCreate",
            User.Identity?.Name,
            $"name={entity.Name} images={uploaded.Count} price={entity.Price} stock={entity.Stock}",
            ct);
        TempData["Status"] = "Product saved.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpGet]
    public async Task<IActionResult> Edit(string rowKey, CancellationToken ct)
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

        ViewBag.ExistingImages = product.AllImageBlobNames.Select(_blobs.GetBlobUrl).ToList();
        ViewBag.RowKey = rowKey;
        SetCategoryOptions(product.Category);
        return View(new ProductCreateViewModel
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            Category = product.Category
        });
    }

    [Authorize(Roles = CustomerEntity.RoleAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(string rowKey, ProductCreateViewModel model, CancellationToken ct)
    {
        var product = await _tables.GetProductAsync(rowKey, ct);
        if (product is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ExistingImages = product.AllImageBlobNames.Select(_blobs.GetBlobUrl).ToList();
            ViewBag.RowKey = rowKey;
            SetCategoryOptions(model.Category ?? product.Category);
            return View(model);
        }

        product.Name = model.Name;
        product.Description = model.Description;
        product.Price = model.Price;
        product.Stock = model.Stock;
        product.Category = model.Category!.Trim();

        var extra = await UploadImagesAsync(model.Images, ct);
        if (!ModelState.IsValid)
        {
            ViewBag.ExistingImages = product.AllImageBlobNames.Select(_blobs.GetBlobUrl).ToList();
            ViewBag.RowKey = rowKey;
            SetCategoryOptions(model.Category ?? product.Category);
            return View(model);
        }

        if (extra.Count > 0)
        {
            var names = product.AllImageBlobNames.Concat(extra).Take(5).ToList();
            product.ImageBlobNames = string.Join('|', names);
            product.ImageBlobName = names[0];
        }

        await _functions.UpsertAsync("Products", product.PartitionKey, product.RowKey, CommerceFormatting.ProductProperties(product), ct);
        await _files.WriteActivityAsync("ProductEdit", User.Identity?.Name, $"id={rowKey} name={product.Name}", ct);
        TempData["Status"] = "Product updated.";
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

        var product = await _tables.GetProductAsync(rowKey, ct);
        if (product is not null)
        {
            foreach (var blob in product.AllImageBlobNames)
            {
                await _functions.DeleteBlobAsync(blob, ct);
            }
        }

        await _functions.DeleteAsync("Products", "PRODUCT", rowKey, ct);
        TempData["Status"] = "Product deleted.";
        return RedirectToAction(nameof(Manage));
    }

    private void SetCategoryOptions(string? extra = null)
    {
        var list = Categories.ToList();
        if (!string.IsNullOrWhiteSpace(extra)
            && !list.Contains(extra, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(extra.Trim());
        }

        ViewBag.Categories = list;
    }

    private async Task<List<string>> UploadImagesAsync(IEnumerable<IFormFile>? images, CancellationToken ct)
    {
        var uploaded = new List<string>();
        if (images is null)
        {
            return uploaded;
        }

        foreach (var image in images.Where(f => f.Length > 0).Take(5))
        {
            try
            {
                await using var stream = image.OpenReadStream();
                var blobName = await _functions.WriteBlobAsync(stream, image.FileName, image.ContentType ?? "application/octet-stream", ct);
                uploaded.Add(blobName);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(ProductCreateViewModel.Images), ex.Message);
                return uploaded;
            }
        }

        return uploaded;
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
