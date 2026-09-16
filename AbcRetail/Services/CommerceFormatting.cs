using System.Security.Cryptography;
using System.Text;
using AbcRetail.Models;

namespace AbcRetail.Services;

public static class CommerceFormatting
{
    public static string OrderQueueMessage(string productId, string productName, string? imageName, string email, int qty, string orderId)
    {
        var imagePart = string.IsNullOrWhiteSpace(imageName) ? "none" : imageName;
        return $"Processing order|{productId}|{productName}|{imagePart}|{email}|{DateTime.UtcNow:O}|{qty}|{orderId}";
    }

    public static string InventoryQueueMessage(string productId, string productName, int stock, string? imageName, string orderId)
    {
        var imagePart = string.IsNullOrWhiteSpace(imageName) ? "none" : imageName;
        return $"Inventory update|{productId}|{productName}|{stock}|{imagePart}|{DateTime.UtcNow:O}|{orderId}";
    }

    public static Dictionary<string, object?> CustomerProperties(CustomerEntity c) => new()
    {
        ["FirstName"] = c.FirstName,
        ["LastName"] = c.LastName,
        ["Email"] = c.Email,
        ["Phone"] = c.Phone,
        ["City"] = c.City,
        ["AddressLine"] = c.AddressLine,
        ["PostalCode"] = c.PostalCode,
        ["PasswordHash"] = c.PasswordHash,
        ["Role"] = c.Role,
        ["EmailConfirmed"] = c.EmailConfirmed,
        ["EmailConfirmToken"] = c.EmailConfirmToken,
        ["EmailConfirmExpiresUtc"] = c.EmailConfirmExpiresUtc
    };

    public static Dictionary<string, object?> ProductProperties(ProductEntity p) => new()
    {
        ["Name"] = p.Name,
        ["Description"] = p.Description,
        ["Price"] = p.Price,
        ["Stock"] = p.Stock,
        ["Category"] = p.Category,
        ["ImageBlobName"] = p.ImageBlobName,
        ["ImageBlobNames"] = p.ImageBlobNames
    };

    public static Dictionary<string, object?> OrderProperties(OrderEntity o) => new()
    {
        ["CustomerEmail"] = o.CustomerEmail,
        ["Status"] = o.Status,
        ["PaymentStatus"] = o.PaymentStatus,
        ["PaymentReference"] = o.PaymentReference,
        ["CardLast4"] = o.CardLast4,
        ["Subtotal"] = o.Subtotal,
        ["Total"] = o.Total,
        ["ShipFirstName"] = o.ShipFirstName,
        ["ShipLastName"] = o.ShipLastName,
        ["ShipPhone"] = o.ShipPhone,
        ["ShipCity"] = o.ShipCity,
        ["AddressLine"] = o.AddressLine,
        ["PostalCode"] = o.PostalCode,
        ["LineItemsJson"] = o.LineItemsJson,
        ["IdempotencyKey"] = o.IdempotencyKey,
        ["StockDeducted"] = o.StockDeducted,
        ["CreatedUtc"] = o.CreatedUtc
    };

    public static string IdempotencyKey(string email, IEnumerable<CartLineViewModel> lines, CheckoutShippingViewModel shipping)
    {
        var cartPart = string.Join(",", lines.OrderBy(l => l.ProductRowKey).Select(l => $"{l.ProductRowKey}:{l.Quantity}"));
        var raw = $"{CustomerEntity.NormalizeEmail(email)}|{cartPart}|{shipping.AddressLine}|{shipping.PostalCode}|{shipping.City}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    public static string DigitsOnly(string? card) =>
        new((card ?? string.Empty).Where(char.IsDigit).ToArray());

    public static (bool Success, string Outcome) SimulateGateway(string digits)
    {
        if (digits.StartsWith("4242", StringComparison.Ordinal))
        {
            return (true, "succeeded");
        }

        if (digits.StartsWith("4000000000000002", StringComparison.Ordinal) || digits.StartsWith("4000", StringComparison.Ordinal))
        {
            return (false, "failed");
        }

        return (false, "failed");
    }
}
