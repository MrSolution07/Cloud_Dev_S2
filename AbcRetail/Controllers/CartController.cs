using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cart;
    private readonly ICartOwner _owner;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;

    public CartController(ICartService cart, ICartOwner owner, IAzureStorageGate gate, IFileStorageService files)
    {
        _cart = cart;
        _owner = owner;
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

        return View(await _cart.GetAsync(_owner.CurrentKey(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string productRowKey, int quantity = 1, string? returnUrl = null, CancellationToken ct = default)
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            TempData["Error"] = "Use a customer account to shop.";
            return RedirectToAction("Index", "Products");
        }

        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var key = _owner.CurrentKey(createGuest: true);
        var error = await _cart.AddAsync(key, productRowKey, quantity, ct);
        if (error is not null)
        {
            await _files.WriteActivityAsync("CartAddFail", key, error, ct);
            TempData["Error"] = error;
        }
        else
        {
            await _files.WriteActivityAsync("CartAdd", key, $"product={productRowKey} qty={quantity}", ct);
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
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return RedirectToAction("Index", "Home");
        }

        var warning = await _cart.UpdateQuantityAsync(_owner.CurrentKey(), productRowKey, quantity, ct);
        TempData[warning is not null ? "Error" : "Status"] = warning ?? "Cart updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string productRowKey, CancellationToken ct)
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return RedirectToAction("Index", "Home");
        }

        await _cart.RemoveAsync(_owner.CurrentKey(), productRowKey, ct);
        TempData["Status"] = "Item removed.";
        return RedirectToAction(nameof(Index));
    }
}
