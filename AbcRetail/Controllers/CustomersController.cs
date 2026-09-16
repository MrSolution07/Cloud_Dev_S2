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
    private readonly IFunctionGateway _functions;
    private readonly PasswordHasher<CustomerEntity> _hasher = new();

    public CustomersController(ITableStorageService tables, IAzureStorageGate gate, IFileStorageService files, IFunctionGateway functions)
    {
        _tables = tables;
        _gate = gate;
        _files = files;
        _functions = functions;
    }

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var customers = await _tables.GetCustomersAsync(ct);
        if (!string.IsNullOrWhiteSpace(q))
        {
            customers = customers.Where(c =>
                c.Email.Contains(q, StringComparison.OrdinalIgnoreCase)
                || c.FirstName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || c.LastName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.Query = q;
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
        entity.PartitionKey = "USER";
        entity.RowKey = CustomerEntity.NormalizeEmail(entity.Email);
        entity.Email = entity.RowKey;
        entity.PasswordHash = _hasher.HashPassword(entity, model.Password);

        await _functions.UpsertAsync("Customers", entity.PartitionKey, entity.RowKey, CommerceFormatting.CustomerProperties(entity), ct);
        await _files.WriteActivityAsync(
            "CustomerCreate",
            User.Identity?.Name,
            $"created={entity.Email} role={role}",
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

        var customers = await _tables.GetCustomersAsync(ct);
        var target = customers.FirstOrDefault(c => string.Equals(c.RowKey, rowKey, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            TempData["Error"] = "Account not found.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(target.Email, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "You cannot delete the account you are signed in with.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(target.Role, CustomerEntity.RoleAdmin, StringComparison.OrdinalIgnoreCase)
            && customers.Count(c => c.Role == CustomerEntity.RoleAdmin) <= 1)
        {
            TempData["Error"] = "Cannot delete the last admin account.";
            return RedirectToAction(nameof(Index));
        }

        await _functions.DeleteAsync("Customers", target.PartitionKey, target.RowKey, ct);
        TempData["Status"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }
}
