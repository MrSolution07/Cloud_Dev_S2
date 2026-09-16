using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ICartService _cart;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;

    public CartController(ICartService cart, IAzureStorageGate gate, IFileStorageService files)
    {
        _cart = cart;
        _gate = gate;
        _files = files;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return RedirectToAction("Index", "Home");
        }

        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var email = User.Identity?.Name ?? string.Empty;
        return View(await _cart.GetAsync(email, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string productRowKey, int quantity = 1, string? returnUrl = null, CancellationToken ct = default)
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            TempData["Error"] = "Admin accounts manage the shop; sign in as a customer to buy.";
            return RedirectToAction("Index", "Products");
        }

        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var email = User.Identity?.Name ?? string.Empty;
        var error = await _cart.AddAsync(email, productRowKey, quantity, ct);
        if (error is not null)
        {
            await _files.WriteActivityAsync("CartAddFail", email, error, ct);
            TempData["Error"] = error;
        }
        else
        {
            await _files.WriteActivityAsync("CartAdd", email, $"product={productRowKey} qty={quantity}", ct);
            TempData["Status"] = "Added to cart.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string productRowKey, int quantity, CancellationToken ct)
    {
        var email = User.Identity?.Name ?? string.Empty;
        var warning = await _cart.UpdateQuantityAsync(email, productRowKey, quantity, ct);
        if (warning is not null)
        {
            TempData["Error"] = warning;
        }
        else
        {
            TempData["Status"] = "Cart updated.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string productRowKey, CancellationToken ct)
    {
        var email = User.Identity?.Name ?? string.Empty;
        await _cart.RemoveAsync(email, productRowKey, ct);
        TempData["Status"] = "Item removed.";
        return RedirectToAction(nameof(Index));
    }
}
