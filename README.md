# ABC Retail — CLDV7112 Project 1

**Author:** Christian Bulabula Emungu ([@MrSolution07](https://github.com/MrSolution07))

ASP.NET Core MVC app for **ABC Retail** using all four Azure Storage services:


| Service            | Feature                                                          |
| ------------------ | ---------------------------------------------------------------- |
| Azure Tables       | Customer/Admin login profiles (Customers table) + Products       |
| Azure Blob Storage | Product images — multiple per product (`product-images`)         |
| Azure Queues       | Orders (`order-processing`) + inventory (`inventory-management`) |
| Azure Files        | Log files (`applogs` / `logs`)                                   |


UI: liquid-glass outdoor look (CSS glassmorphism; kayak sites as visual inspiration only — catalog stays general retail), lightweight images (≤1 MB each, up to 5 per product).

Prices are shown in **ZAR (R)**.

## Accounts & roles

- **Customer** — self-registers at `/Account/Register`. Can browse the shop, view product details/galleries, add to cart, and edit their own profile.
- **Admin** — full access: manage products (multi-image upload, description, price, stock), view all customers, process the order/inventory queues, and view logs.

A default Admin account is seeded automatically the first time the app runs against a configured storage account:

```
Email:    admin@abcretail.local
Password: Admin@12345
```

Change this password (or delete/recreate the row in the `Customers` table) before sharing the app.

## Quick start (local testing — no Azure App Service deploy yet)

```bash
cd AbcRetail
dotnet restore
dotnet user-secrets set "AzureStorage:ConnectionString" "<your-storage-connection-string>"
dotnet run
```

Open the HTTPS URL from the console (see `Properties/launchSettings.json`). If another `dotnet run` is already using the port, stop it first (`Ctrl+C` in its terminal, or kill the process) before starting a new one.

Test locally as both roles (register a customer, log in as the seeded admin) before deploying to Azure App Service.

## Azure setup

Step-by-step portal + CLI guides live in `[docs/azure/](docs/azure/00-overview.md)`.

**Must use StorageV2** (general-purpose v2), not a Blob-only account.

## Project layout

```
AbcRetail/          MVC web app (.NET 10)
docs/azure/         How to create Azure resources
```



## Deploy

See `[docs/azure/07-app-service-deploy.md](docs/azure/07-app-service-deploy.md)`.

App Setting name: `AzureStorage__ConnectionString`

## Submission

See `[docs/azure/09-screenshot-checklist.md](docs/azure/09-screenshot-checklist.md)` for rubric screenshots (≥5 records per service).

## Note on target framework

Scaffolded with the installed SDK templates as **net10.0**. If your campus App Service only offers .NET 8, change `<TargetFramework>` in `AbcRetail.csproj` to `net8.0` (requires the .NET 8 targeting pack) and select .NET 8 in App Service.