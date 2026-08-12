using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

/// <summary>Admin-only view of customer profiles stored in Azure Table Storage (Customers table).
/// Customers can also self-register via Account/Register.</summary>
[Authorize(Roles = CustomerEntity.RoleAdmin)]
public class CustomersController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly PasswordHasher<CustomerEntity> _hasher = new();

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

        return View(new AdminCustomerCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCustomerCreateViewModel model, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var role = string.Equals(model.Role, CustomerEntity.RoleAdmin, StringComparison.OrdinalIgnoreCase)
            ? CustomerEntity.RoleAdmin
            : CustomerEntity.RoleCustomer;

        var existing = await _tables.GetUserByEmailAsync(model.Email, ct);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var entity = new CustomerEntity
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Phone = model.Phone,
            City = model.City,
            Role = role
        };
        entity.PasswordHash = _hasher.HashPassword(entity, model.Password);

        await _tables.AddCustomerAsync(entity, ct);
        await _files.WriteLogAsync(
            $"customer-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log",
            $"Admin created {role} {entity.Email} at {DateTime.UtcNow:O}",
            ct);

        TempData["Status"] = $"Customer saved to Table Storage ({role}).";
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
