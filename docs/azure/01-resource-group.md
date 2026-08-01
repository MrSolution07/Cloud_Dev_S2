# 01 — Create resource group

Official guide: [Manage resource groups](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/manage-resource-groups-portal)

## Portal

1. Open [Azure Portal](https://portal.azure.com) → **Resource groups** → **Create**.
2. Subscription: your student/campus subscription.
3. Resource group name: `rg-abc-retail-cldv7112`
4. Region: campus default (often `South Africa North` / `West Europe`).
5. **Review + create** → **Create**.

## Azure CLI

```bash
az group create \
  --name rg-abc-retail-cldv7112 \
  --location southafricanorth
```

## Screenshot point

Resource group Overview blade showing name + region.
