using System.Text.Json;
using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private const string ShippingKey = "checkout-shipping";

    private readonly ICartService _cart;
    private readonly ITableStorageService _tables;
    private readonly IFunctionGateway _functions;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly IBlobStorageService _blobs;

    public CheckoutController(
        ICartService cart,
        ITableStorageService tables,
        IFunctionGateway functions,
        IAzureStorageGate gate,
        IFileStorageService files,
        IBlobStorageService blobs)
    {
        _cart = cart;
        _tables = tables;
        _functions = functions;
        _gate = gate;
        _files = files;
        _blobs = blobs;
    }

    public IActionResult Index() => RedirectToAction(nameof(Shipping));

    [HttpGet]
    public async Task<IActionResult> Shipping(CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var email = User.Identity!.Name!;
        var cart = await _cart.GetAsync(email, ct);
        if (!cart.CanCheckout)
        {
            TempData["Error"] = cart.Lines.Count == 0
                ? "Your cart is empty."
                : "Fix stock issues in your cart before checkout.";
            return RedirectToAction("Index", "Cart");
        }

        var user = await _tables.GetUserByEmailAsync(email, ct);
        var model = ReadShipping() ?? new CheckoutShippingViewModel
        {
            FirstName = user?.FirstName ?? string.Empty,
            LastName = user?.LastName ?? string.Empty,
            Phone = user?.Phone,
            City = user?.City ?? string.Empty,
            AddressLine = user?.AddressLine ?? string.Empty,
            PostalCode = user?.PostalCode ?? string.Empty
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shipping(CheckoutShippingViewModel model, CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        SaveShipping(model);
        var email = User.Identity!.Name!;
        var user = await _tables.GetUserByEmailAsync(email, ct);
        if (user is not null)
        {
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Phone = model.Phone;
            user.City = model.City;
            user.AddressLine = model.AddressLine;
            user.PostalCode = model.PostalCode;
            await _functions.UpsertAsync("Customers", user.PartitionKey, user.RowKey, CommerceFormatting.CustomerProperties(user), ct);
        }

        return RedirectToAction(nameof(Summary));
    }

    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var shipping = ReadShipping();
        if (shipping is null)
        {
            return RedirectToAction(nameof(Shipping));
        }

        var cart = await _cart.GetAsync(User.Identity!.Name!, ct);
        if (!cart.CanCheckout)
        {
            TempData["Error"] = "Your cart cannot be checked out in its current state.";
            return RedirectToAction("Index", "Cart");
        }

        KeepShipping();
        return View(new CheckoutSummaryViewModel { Cart = cart, Shipping = shipping });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var shipping = ReadShipping();
        if (shipping is null)
        {
            return RedirectToAction(nameof(Shipping));
        }

        var email = User.Identity!.Name!;
        var cart = await _cart.GetAsync(email, ct);
        if (!cart.CanCheckout)
        {
            TempData["Error"] = "Your cart cannot be checked out in its current state.";
            return RedirectToAction("Index", "Cart");
        }

        var key = CommerceFormatting.IdempotencyKey(email, cart.Lines, shipping);
        var existing = await _tables.FindOrderByIdempotencyAsync(email, key, ct);
        if (existing is not null)
        {
            return RedirectToAction(nameof(Pay), new { orderId = existing.RowKey });
        }

        var order = new OrderEntity
        {
            CustomerEmail = CustomerEntity.NormalizeEmail(email),
            Status = OrderEntity.StatusPending,
            PaymentStatus = OrderEntity.PaymentInitiated,
            Subtotal = cart.Subtotal,
            Total = cart.Total,
            ShipFirstName = shipping.FirstName,
            ShipLastName = shipping.LastName,
            ShipPhone = shipping.Phone,
            ShipCity = shipping.City,
            AddressLine = shipping.AddressLine,
            PostalCode = shipping.PostalCode,
            IdempotencyKey = key,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        order.SetLines(cart.Lines.Select(l => new OrderLineItem
        {
            ProductRowKey = l.ProductRowKey,
            Name = l.Name,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice
        }));

        // Prefer blob names from live products (URL parse is a fallback).
        foreach (var line in order.GetLines())
        {
            var product = await _tables.GetProductAsync(line.ProductRowKey, ct);
            if (product is not null)
            {
                line.ImageBlobName = product.PrimaryImageBlobName;
                line.UnitPrice = product.Price;
            }
        }

        order.SetLines(order.GetLines());
        order.Subtotal = order.GetLines().Sum(l => l.UnitPrice * l.Quantity);
        order.Total = order.Subtotal;

        await _functions.UpsertAsync("Orders", order.PartitionKey, order.RowKey, CommerceFormatting.OrderProperties(order), ct);
        await _files.WriteActivityAsync("CheckoutPlace", email, $"order={order.RowKey} total={order.Total}", ct);
        return RedirectToAction(nameof(Pay), new { orderId = order.RowKey });
    }

    [HttpGet]
    public async Task<IActionResult> Pay(string orderId, CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var order = await LoadOwnOrderAsync(orderId, ct);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status == OrderEntity.StatusPaid || order.PaymentStatus == OrderEntity.PaymentSucceeded)
        {
            return RedirectToAction(nameof(Confirmation), new { orderId });
        }

        return View(new PaymentViewModel
        {
            OrderId = order.RowKey,
            Amount = order.Total,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            CardholderName = $"{order.ShipFirstName} {order.ShipLastName}".Trim()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string orderId, PaymentViewModel model, string outcome, CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var order = await LoadOwnOrderAsync(orderId, ct);
        if (order is null)
        {
            return NotFound();
        }

        if (order.PaymentStatus == OrderEntity.PaymentSucceeded)
        {
            return RedirectToAction(nameof(Confirmation), new { orderId });
        }

        model.OrderId = orderId;
        model.Amount = order.Total;

        if (string.Equals(outcome, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            await FinalisePaymentAsync(order, OrderEntity.StatusCancelled, OrderEntity.PaymentCancelled, null, null, ct);
            TempData["Error"] = "Payment cancelled. No charge was made.";
            return RedirectToAction("Index", "Cart");
        }

        if (!ModelState.IsValid && !string.Equals(outcome, "fail", StringComparison.OrdinalIgnoreCase))
        {
            return View(model);
        }

        var digits = CommerceFormatting.DigitsOnly(model.CardNumber);
        var simulated = string.Equals(outcome, "fail", StringComparison.OrdinalIgnoreCase)
            ? (Success: false, Outcome: "failed")
            : CommerceFormatting.SimulateGateway(digits);

        if (!simulated.Success)
        {
            await FinalisePaymentAsync(order, OrderEntity.StatusFailed, OrderEntity.PaymentFailed, $"sim_fail_{order.RowKey}", Last4(digits), ct);
            await _functions.WriteFileAsync($"payment-failed-{order.RowKey}.log", $"Payment failed for order {order.RowKey} at {DateTime.UtcNow:O}. No stock deducted.", ct);
            TempData["Error"] = "Payment failed. The simulated gateway declined this test card. Your order was not completed.";
            return RedirectToAction(nameof(Pay), new { orderId });
        }

        var email = User.Identity!.Name!;
        var liveCart = await _cart.GetAsync(email, ct);
        if (!liveCart.CanCheckout)
        {
            await FinalisePaymentAsync(order, OrderEntity.StatusFailed, OrderEntity.PaymentFailed, $"sim_stock_{order.RowKey}", Last4(digits), ct);
            TempData["Error"] = "Stock changed before payment completed. No charge was captured.";
            return RedirectToAction("Index", "Cart");
        }

        if (!order.StockDeducted)
        {
            foreach (var line in order.GetLines())
            {
                var product = await _tables.GetProductAsync(line.ProductRowKey, ct);
                if (product is null || product.Stock < line.Quantity)
                {
                    await FinalisePaymentAsync(order, OrderEntity.StatusFailed, OrderEntity.PaymentFailed, $"sim_stock_{order.RowKey}", Last4(digits), ct);
                    TempData["Error"] = "An item went out of stock during payment. No charge was captured.";
                    return RedirectToAction("Index", "Cart");
                }
            }

            foreach (var line in order.GetLines())
            {
                await _functions.DecrementStockAsync(line.ProductRowKey, line.Quantity, ct);
                var updated = await _tables.GetProductAsync(line.ProductRowKey, ct);
                await _functions.WriteQueueAsync(
                    "order-processing",
                    CommerceFormatting.OrderQueueMessage(line.ProductRowKey, line.Name, line.ImageBlobName, email, line.Quantity, order.RowKey),
                    ct);
                await _functions.WriteQueueAsync(
                    "inventory-management",
                    CommerceFormatting.InventoryQueueMessage(line.ProductRowKey, line.Name, updated?.Stock ?? 0, line.ImageBlobName, order.RowKey),
                    ct);
            }

            order.StockDeducted = true;
        }

        order.PaymentReference = $"sim_{Guid.NewGuid():N}"[..20];
        order.CardLast4 = Last4(digits);
        await FinalisePaymentAsync(order, OrderEntity.StatusPaid, OrderEntity.PaymentSucceeded, order.PaymentReference, order.CardLast4, ct);
        await _functions.WriteFileAsync(
            $"order-{order.RowKey}.log",
            $"Payment succeeded for order {order.RowKey} amount={order.Total:0.00} last4={order.CardLast4} ref={order.PaymentReference} at {DateTime.UtcNow:O}",
            ct);
        await _cart.ClearAsync(email, ct);
        await _files.WriteActivityAsync("PaymentSuccess", email, $"order={order.RowKey} total={order.Total}", ct);
        return RedirectToAction(nameof(Confirmation), new { orderId });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(string orderId, CancellationToken ct)
    {
        var blocked = await GuardAsync(ct);
        if (blocked is not null)
        {
            return blocked;
        }

        var order = await LoadOwnOrderAsync(orderId, ct);
        if (order is null)
        {
            return NotFound();
        }

        ViewBag.BlobUrlResolver = (Func<string, string>)_blobs.GetBlobUrl;
        return View(new OrderDetailsViewModel { Order = order, Lines = order.GetLines() });
    }

    private async Task<IActionResult?> GuardAsync(CancellationToken ct)
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            TempData["Error"] = "Use a customer account to check out.";
            return RedirectToAction("Index", "Home");
        }

        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        return null;
    }

    private async Task<OrderEntity?> LoadOwnOrderAsync(string orderId, CancellationToken ct)
    {
        var order = await _tables.GetOrderAsync(orderId, ct);
        var email = User.Identity?.Name;
        if (order is null || email is null)
        {
            return null;
        }

        return string.Equals(order.CustomerEmail, CustomerEntity.NormalizeEmail(email), StringComparison.OrdinalIgnoreCase) ? order : null;
    }

    private async Task FinalisePaymentAsync(OrderEntity order, string status, string paymentStatus, string? reference, string? last4, CancellationToken ct)
    {
        order.Status = status;
        order.PaymentStatus = paymentStatus;
        order.PaymentReference = reference ?? order.PaymentReference;
        order.CardLast4 = last4 ?? order.CardLast4;
        await _functions.UpsertAsync("Orders", order.PartitionKey, order.RowKey, CommerceFormatting.OrderProperties(order), ct);
    }

    private CheckoutShippingViewModel? ReadShipping()
    {
        if (TempData[ShippingKey] is not string json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CheckoutShippingViewModel>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SaveShipping(CheckoutShippingViewModel model)
    {
        TempData[ShippingKey] = JsonSerializer.Serialize(model);
    }

    private void KeepShipping()
    {
        TempData.Keep(ShippingKey);
    }

    private static string? Last4(string digits) =>
        digits.Length >= 4 ? digits[^4..] : null;
}
