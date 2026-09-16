using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Admin-only: log files stored in Azure Files (share applogs, directory logs).</summary>
[Authorize(Roles = CustomerEntity.RoleAdmin)]
public class LogsController : Controller
{
    private readonly IFileStorageService _files;
    private readonly IAzureStorageGate _gate;
    private readonly IFunctionGateway _functions;

    public LogsController(IFileStorageService files, IAzureStorageGate gate, IFunctionGateway functions)
    {
        _files = files;
        _gate = gate;
        _functions = functions;
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

        await _functions.WriteFileAsync(create.FileName, create.Content, ct);
        TempData["Status"] = $"Log file '{create.FileName}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        await _files.ClearLogsAsync(ct);
        TempData["Status"] = "All log files deleted.";
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
