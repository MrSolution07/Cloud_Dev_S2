using AbcRetail.Models;
using AbcRetail.Options;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace AbcRetail.Services;

/// <summary>Azure Queue Storage: order processing + inventory management messages.</summary>
public interface IQueueStorageService
{
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task SendOrderMessageAsync(string productId, string productName, string? imageName, CancellationToken ct = default);
    Task SendInventoryMessageAsync(string productId, string productName, int stock, string? imageName, CancellationToken ct = default);
    Task<IReadOnlyList<OrderMessageViewModel>> PeekOrderMessagesAsync(int maxMessages = 32, CancellationToken ct = default);
    Task<IReadOnlyList<OrderMessageViewModel>> PeekInventoryMessagesAsync(int maxMessages = 32, CancellationToken ct = default);
    Task<OrderMessageViewModel?> DequeueOneAsync(CancellationToken ct = default);
    Task<int> GetApproximateOrderCountAsync(CancellationToken ct = default);
    Task<int> GetApproximateInventoryCountAsync(CancellationToken ct = default);
}

public sealed class QueueStorageService : IQueueStorageService
{
    private readonly AzureStorageOptions _options;
    private QueueClient? _orderQueue;
    private QueueClient? _inventoryQueue;
    private bool _initialized;

    public QueueStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    private QueueClient OrderQueue =>
        _orderQueue ??= new QueueClient(
            _options.ConnectionString,
            _options.QueueName,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

    private QueueClient InventoryQueue =>
        _inventoryQueue ??= new QueueClient(
            _options.ConnectionString,
            _options.InventoryQueueName,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await OrderQueue.CreateIfNotExistsAsync(cancellationToken: ct);
        await InventoryQueue.CreateIfNotExistsAsync(cancellationToken: ct);
        _initialized = true;
    }

    // Format matches brief example: Processing order + imageName.
    public async Task SendOrderMessageAsync(string productId, string productName, string? imageName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var imagePart = string.IsNullOrWhiteSpace(imageName) ? "none" : imageName;
        var text = $"Processing order|{productId}|{productName}|{imagePart}|{DateTime.UtcNow:O}";
        await OrderQueue.SendMessageAsync(text, cancellationToken: ct);
    }

    // Inventory management queue message (stock change after order processing).
    public async Task SendInventoryMessageAsync(string productId, string productName, int stock, string? imageName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var imagePart = string.IsNullOrWhiteSpace(imageName) ? "none" : imageName;
        var text = $"Inventory update|{productId}|{productName}|{stock}|{imagePart}|{DateTime.UtcNow:O}";
        await InventoryQueue.SendMessageAsync(text, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<OrderMessageViewModel>> PeekOrderMessagesAsync(int maxMessages = 32, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return await PeekAsync(OrderQueue, maxMessages, ct);
    }

    public async Task<IReadOnlyList<OrderMessageViewModel>> PeekInventoryMessagesAsync(int maxMessages = 32, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return await PeekAsync(InventoryQueue, maxMessages, ct);
    }

    public async Task<OrderMessageViewModel?> DequeueOneAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var response = await OrderQueue.ReceiveMessageAsync(cancellationToken: ct);
        var message = response.Value;
        if (message is null)
        {
            return null;
        }

        await OrderQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        return new OrderMessageViewModel
        {
            MessageId = message.MessageId,
            MessageText = message.MessageText,
            InsertedOn = message.InsertedOn,
            ExpiresOn = message.ExpiresOn
        };
    }

    public async Task<int> GetApproximateOrderCountAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var props = await OrderQueue.GetPropertiesAsync(ct);
        return props.Value.ApproximateMessagesCount;
    }

    public async Task<int> GetApproximateInventoryCountAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var props = await InventoryQueue.GetPropertiesAsync(ct);
        return props.Value.ApproximateMessagesCount;
    }

    private static async Task<IReadOnlyList<OrderMessageViewModel>> PeekAsync(QueueClient queue, int maxMessages, CancellationToken ct)
    {
        var response = await queue.PeekMessagesAsync(maxMessages, ct);
        return response.Value.Select(m => new OrderMessageViewModel
        {
            MessageId = m.MessageId,
            MessageText = m.MessageText,
            InsertedOn = m.InsertedOn,
            ExpiresOn = m.ExpiresOn
        }).ToList();
    }
}
