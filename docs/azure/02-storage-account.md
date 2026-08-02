# 02 — Create StorageV2 account

Official guide: [Create a storage account](https://learn.microsoft.com/en-us/azure/storage/common/storage-account-create)

## Why StorageV2

General-purpose **v2** accounts support **Blob + File + Table + Queue** in one place. A BlobStorage-only account cannot satisfy this project.

## Portal

1. **Create a resource** → **Storage account**.
2. Basics:
   - Resource group: `rg-abc-retail-cldv7112`
   - Storage account name: `stabc{studentnumber}` (globally unique, lowercase)
   - Region: same as resource group
   - Performance: **Standard**
   - Redundancy: **Locally-redundant storage (LRS)**
3. Advanced (important):
   - **Allow enabling anonymous access on individual containers**: Enabled (for product images)
   - **Allow storage account key access**: Enabled
4. Review + create.

## Azure CLI

```bash
az storage account create \
  --name stabcSTUDENTNUMBER \
  --resource-group rg-abc-retail-cldv7112 \
  --location southafricanorth \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Hot \
  --allow-blob-public-access true \
  --allow-shared-key-access true
```

## Copy connection string

Portal → Storage account → **Security + networking** → **Access keys** → **key1** → **Connection string** → Show / Copy.

Or:

```bash
az storage account show-connection-string \
  --name stabcSTUDENTNUMBER \
  --resource-group rg-abc-retail-cldv7112 \
  --query connectionString -o tsv
```

Paste into User Secrets — see [08-connection-strings-and-secrets.md](08-connection-strings-and-secrets.md).

## Screenshot points

- Storage account Overview (kind StorageV2 / Standard)
- Access keys blade (blur the secret values in the Word doc if required)
