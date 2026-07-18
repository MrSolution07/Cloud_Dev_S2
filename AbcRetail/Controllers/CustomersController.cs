using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Customer profiles stored in Azure Table Storage (Customers table).</summary>
public class CustomersController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;

    public CustomersController(ITableStorageService tables, IAzureStorageGate gate, IFileStorageService files)
    {
        _tables = tables;
        _gate = gate;
        _files = files;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var customers = await _tables.GetCustomersAsync(ct);
        return View(customers);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        return View(new CustomerEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerEntity model, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _tables.AddCustomerAsync(model, ct);
        await _files.WriteLogAsync(
            $"customer-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Created customer {model.Email} at {DateTime.UtcNow:O}",
            ct);
        TempData["Status"] = "Customer saved to Azure Table Storage.";
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

        await _tables.DeleteCustomerAsync(rowKey, ct);
        TempData["Status"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }
}
