# 05 — Azure Queues for orders and inventory

Official guide: [Azure Queue storage overview](https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction)

## Portal

1. Storage account → **Queues** → **+ Queue**.
2. Create **two** queues:
   - `order-processing`
   - `inventory-management`

## App behaviour

- **Shop → Add to cart** enqueues: `Processing order|{productId}|{productName}|{imageName}|{utc}`
- **Orders → Process oldest order** dequeues one order, decrements Table stock, then enqueues: `Inventory update|{productId}|{productName}|{stock}|{imageName}|{utc}`
- SDK uses `QueueMessageEncoding.Base64` for reliable portal/SDK interop
- **Peek** shows messages without deleting (use this for screenshots)
- Process/dequeue **after** order-queue screenshots if you need ≥5 visible

## Seed tip

1. Enqueue until `order-processing` ≥ 5, screenshot portal + Orders page.
2. Process five orders so `inventory-management` also has ≥ 5, screenshot again.

## Screenshot points

- Queue `order-processing` ≥5 messages
- Queue `inventory-management` ≥5 messages
- App Orders page showing raw text + `imageName` for both queues
