namespace AbcRetail.Models;

/// <summary>Admin Command Centre aggregates across Table, Blob, Queue, and File storage.</summary>
public class DashboardViewModel
{
    public int CustomerCount { get; set; }
    public int ProductImageCount { get; set; }
    public int QueueMessageCount { get; set; }
    public int LogFileCount { get; set; }
    public int OrderCount { get; set; }

    public IReadOnlyList<CustomerEntity> RecentCustomers { get; set; } = Array.Empty<CustomerEntity>();
    public IReadOnlyList<OrderMessageViewModel> RecentOrders { get; set; } = Array.Empty<OrderMessageViewModel>();
    public IReadOnlyList<OrderEntity> RecentTableOrders { get; set; } = Array.Empty<OrderEntity>();
    public IReadOnlyList<LogFileViewModel> RecentLogs { get; set; } = Array.Empty<LogFileViewModel>();
    public IReadOnlyList<ProductEntity> RecentProducts { get; set; } = Array.Empty<ProductEntity>();
}
