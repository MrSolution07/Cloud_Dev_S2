using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AbcRetail.Controllers;

/// <summary>Home / featured products (reads Azure Tables + Blob URLs when configured).</summary>
public class HomeController : Controller
{
    private readonly IAzureStorageGate _gate;
    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;

    public HomeController(IAzureStorageGate gate, ITableStorageService tables, IBlobStorageService blobs)
    {
        _gate = gate;
        _tables = tables;
        _blobs = blobs;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.StorageReady = _gate.IsConfigured;
        ViewBag.StorageMessage = _gate.MissingReason;

        IReadOnlyList<ProductEntity> featured = Array.Empty<ProductEntity>();
        if (_gate.IsConfigured)
        {
            var products = await _tables.GetProductsAsync(ct);
            foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.ImageBlobName)))
            {
                product.ImageUrl = _blobs.GetBlobUrl(product.ImageBlobName!);
            }
            featured = products.Take(4).ToList();
        }

        return View(featured);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
