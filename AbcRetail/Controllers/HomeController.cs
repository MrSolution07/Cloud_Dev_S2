using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AbcRetail.Controllers;

/// <summary>Home: storefront for guests/customers; Command Centre dashboard for Admin.</summary>
public class HomeController : Controller
{
    private readonly IAzureStorageGate _gate;
    private readonly ITableStorageService _tables;
    private readonly IBlobStorageService _blobs;
    private readonly IQueueStorageService _queues;
    private readonly IFileStorageService _files;

    public HomeController(
        IAzureStorageGate gate,
        ITableStorageService tables,
        IBlobStorageService blobs,
        IQueueStorageService queues,
        IFileStorageService files)
    {
        _gate = gate;
        _tables = tables;
        _blobs = blobs;
        _queues = queues;
        _files = files;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.StorageReady = _gate.IsConfigured;
        ViewBag.StorageMessage = _gate.MissingReason;

        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return await DashboardAsync(ct);
        }

        IReadOnlyList<ProductEntity> featured = Array.Empty<ProductEntity>();
        if (_gate.IsConfigured)
        {
            var products = await _tables.GetProductsAsync(ct);
            foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.PrimaryImageBlobName)))
            {
                product.ImageUrl = _blobs.GetBlobUrl(product.PrimaryImageBlobName!);
            }
            featured = products.Take(4).ToList();
        }

        return View(featured);
    }

    private async Task<IActionResult> DashboardAsync(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var customers = await _tables.GetCustomersAsync(ct);
        var products = await _tables.GetProductsAsync(ct);
        var blobs = await _blobs.ListBlobNamesAsync(ct);
        var orders = await _queues.PeekOrderMessagesAsync(maxMessages: 8, ct: ct);
        var logs = await _files.ListLogsAsync(ct);

        foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.PrimaryImageBlobName)))
        {
            product.ImageUrl = _blobs.GetBlobUrl(product.PrimaryImageBlobName!);
        }

        var model = new DashboardViewModel
        {
            CustomerCount = customers.Count,
            ProductImageCount = blobs.Count,
            QueueMessageCount = await _queues.GetApproximateOrderCountAsync(ct),
            LogFileCount = logs.Count,
            RecentCustomers = customers
                .OrderByDescending(c => c.Timestamp)
                .Take(5)
                .ToList(),
            RecentOrders = orders.Take(5).ToList(),
            RecentLogs = logs.Take(5).ToList(),
            RecentProducts = products.Take(6).ToList()
        };

        return View("Dashboard", model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
