# Azure setup overview — ABC Retail (CLDV7112)

Create **one** Azure Storage account that exposes all four services used by the app:

| Azure service | App usage | Resource name |
|---------------|-----------|---------------|
| Tables | Customers + Products | tables `Customers`, `Products` |
| Blob | Product images | container `product-images` |
| Queue | Order processing + inventory management | queues `order-processing`, `inventory-management` |
| Files | Log files | share `applogs` |

## Critical rules

1. Account kind must be **StorageV2 (General-purpose v2)** — not BlobStorage-only.
2. SKU: **Standard_LRS** (cost-effective for coursework).
3. Keep **Allow storage account key access** enabled (connection-string auth).
4. Prefer enabling **Allow Blob public access** so product images render in the browser.
5. Never commit the connection string. Use User Secrets locally and App Setting `AzureStorage__ConnectionString` on App Service.

## Suggested names

- Resource group: `rg-abc-retail-cldv7112`
- Storage account: `stabc{studentnumber}` (3–24 lowercase letters/numbers, globally unique)
- App Service: `{studentnumber}` (matches brief URL style)

## Create order

1. [01-resource-group.md](01-resource-group.md)
2. [02-storage-account.md](02-storage-account.md)
3. [03-tables.md](03-tables.md) (optional — app can auto-create)
4. [04-blob-container.md](04-blob-container.md)
5. [05-queue.md](05-queue.md)
6. [06-file-share.md](06-file-share.md)
7. [08-connection-strings-and-secrets.md](08-connection-strings-and-secrets.md)
8. Run the MVC app and seed ≥5 records per service
9. [07-app-service-deploy.md](07-app-service-deploy.md)
10. [09-screenshot-checklist.md](09-screenshot-checklist.md)

## Official docs

- [Create a storage account](https://learn.microsoft.com/en-us/azure/storage/common/storage-account-create)
- [Azure Storage overview](https://learn.microsoft.com/en-us/azure/storage/common/storage-introduction)
