namespace AbcRetail.Models;

public class OrderMessageViewModel
{
    public string MessageId { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTimeOffset? InsertedOn { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
}

public class OrdersIndexViewModel
{
    public IReadOnlyList<ProductEntity> Products { get; set; } = [];
    public IReadOnlyList<OrderMessageViewModel> PeekedOrderMessages { get; set; } = [];
    public IReadOnlyList<OrderMessageViewModel> PeekedInventoryMessages { get; set; } = [];
    public int ApproximateOrderCount { get; set; }
    public int ApproximateInventoryCount { get; set; }
    public IReadOnlyList<OrderEntity> TableOrders { get; set; } = [];
    public string? StatusFilter { get; set; }
    public string? Query { get; set; }
}
