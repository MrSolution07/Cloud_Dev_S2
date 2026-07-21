using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Log files stored in Azure Files (share applogs, directory logs).</summary>
public class LogsController : Controller
{
    private readonly IFileStorageService _files;
    private readonly IAzureStorageGate _gate;

    public LogsController(IFileStorageService files, IAzureStorageGate gate)
    {
        _files = files;
        _gate = gate;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var model = new LogsIndexViewModel
        {
            Files = await _files.ListLogsAsync(ct),
            Create = new CreateLogViewModel
            {
                FileName = $"manual-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
                Content = $"Inventory check at {DateTime.UtcNow:O}"
            }
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Create")] CreateLogViewModel create, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            var model = new LogsIndexViewModel
            {
                Files = await _files.ListLogsAsync(ct),
                Create = create
            };
            return View("Index", model);
        }

        await _files.WriteLogAsync(create.FileName, create.Content, ct);
        TempData["Status"] = $"Log file '{create.FileName}' stored in Azure Files.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(string fileName, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var result = await _files.DownloadLogAsync(fileName, ct);
        if (result is null)
        {
            return NotFound();
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
