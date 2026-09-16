namespace AbcRetail.Models;

public class CartLineViewModel
{
    public string ProductRowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Stock { get; set; }
    public double UnitPrice { get; set; }
    public double LineTotal => UnitPrice * Quantity;
    public string? ImageUrl { get; set; }
    public bool Unavailable { get; set; }
    public bool QtyExceedsStock { get; set; }
}

public class CartViewModel
{
    public List<CartLineViewModel> Lines { get; set; } = [];
    public double Subtotal { get; set; }
    public double Total { get; set; }
    public int ItemCount => Lines.Sum(l => l.Quantity);
    public bool CanCheckout => Lines.Count > 0 && Lines.All(l => !l.Unavailable && !l.QtyExceedsStock && l.Quantity > 0);
}

public class CheckoutShippingViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Phone, System.ComponentModel.DataAnnotations.StringLength(30)]
    public string? Phone { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(120)]
    public string City { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200)]
    public string AddressLine { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;
}

public class CheckoutSummaryViewModel
{
    public CartViewModel Cart { get; set; } = new();
    public CheckoutShippingViewModel Shipping { get; set; } = new();
}

public class PaymentViewModel
{
    public string OrderId { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Status { get; set; } = OrderEntity.StatusPending;
    public string PaymentStatus { get; set; } = OrderEntity.PaymentInitiated;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string CardholderName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[0-9 ]{13,23}$", ErrorMessage = "Enter a card number (digits only).")]
    public string CardNumber { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Use MM/YY.")]
    public string Expiry { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVC is 3 or 4 digits.")]
    public string Cvc { get; set; } = string.Empty;
}

public class OrderDetailsViewModel
{
    public OrderEntity Order { get; set; } = new();
    public List<OrderLineItem> Lines { get; set; } = [];
    public bool IsAdmin { get; set; }
    public string? ImageBase { get; set; }
}
