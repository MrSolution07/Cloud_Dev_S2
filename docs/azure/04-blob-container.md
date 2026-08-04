# 04 — Blob container for product images

Official guides:

- [Create a container](https://learn.microsoft.com/en-us/azure/storage/blobs/blob-containers-portal)
- [Manage anonymous read access](https://learn.microsoft.com/en-us/azure/storage/blobs/anonymous-read-access-configure)

## Portal

1. Storage account → **Containers** → **+ Container**.
2. Name: `product-images`
3. Anonymous access level: **Blob (anonymous read access for blobs only)**
4. Create.

If anonymous access is greyed out: Storage account → **Configuration** → enable **Allow Blob anonymous access**, Save, retry.

## Azure CLI

```bash
az storage container create \
  --account-name stabcSTUDENTNUMBER \
  --name product-images \
  --public-access blob \
  --auth-mode login
```

## App behaviour

- Uploads from **Products → Add product**
- Max size **1 MB** (JPEG/PNG/WebP/GIF) for fast pages
- App can create the container if missing

## Screenshot points

- Container `product-images` listed
- ≥5 blobs visible in the container
- Product page showing images rendered from blob URLs
