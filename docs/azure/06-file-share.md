# 06 — Azure Files share for logs

Official guide: [Create an Azure file share](https://learn.microsoft.com/en-us/azure/storage/files/storage-how-to-create-file-share)

## Portal

1. Storage account → **File shares** → **+ File share**.
2. Name: `applogs`
3. Tier: Transaction optimized / Hot (default is fine for coursework)
4. Create.

## App behaviour

- Uses `Azure.Storage.Files.Shares` SDK (`ShareClient`) — **no SMB mount required**
- Directory: `logs/`
- Creating customers/products/orders also writes log files
- **Logs** page can create/list/download by filename

## Azure CLI

```bash
az storage share create \
  --account-name stabcSTUDENTNUMBER \
  --name applogs
```

## Screenshot points

- File share `applogs`
- `logs/` directory with ≥5 `.log` files
- App Logs page listing the same names
