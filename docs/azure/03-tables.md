# 03 — Azure Tables

Official guide: [Azure Table Storage overview](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview)

The MVC app auto-creates tables `Customers` and `Products` on first use. You can also create them in the portal for screenshots.

## Portal

1. Storage account → **Data storage** → **Tables** (or Storage browser → Tables).
2. **+ Table** → name `Customers` → OK.
3. **+ Table** → name `Products` → OK.

## Verify from the app

1. Open **Customers** → add ≥5 customers.
2. Open **Products** → add ≥5 products.
3. Portal → Tables → open each table → confirm ≥5 entities.

## Entity shape (app)

- Customers: `PartitionKey=CUSTOMER`, `RowKey=guid`, FirstName, LastName, Email, Phone, City
- Products: `PartitionKey=PRODUCT`, `RowKey=guid`, Name, Description, Price, Stock, ImageBlobName

## Screenshot points

- Tables list showing `Customers` and `Products`
- Entity list with ≥5 rows in each table
