# ABC Retail — CLDV7112 Project 1

**Author:** Christian Bulabula Emungu ([@MrSolution07](https://github.com/MrSolution07))

ASP.NET Core MVC app for **ABC Retail** using all four Azure Storage services:

| Service | Feature |
|---------|---------|
| Azure Tables | Customers + Products |
| Azure Blob Storage | Product images (`product-images`) |
| Azure Queues | Orders (`order-processing`) + inventory (`inventory-management`) |
| Azure Files | Log files (`applogs` / `logs`) |

UI: liquid-glass (CSS glassmorphism), lightweight images (≤1 MB).

## Quick start

```bash
cd AbcRetail
dotnet restore
dotnet user-secrets set "AzureStorage:ConnectionString" "<your-storage-connection-string>"
dotnet run
```

Open the HTTPS URL from the console (see `Properties/launchSettings.json`).

## Azure setup

Step-by-step portal + CLI guides live in [`docs/azure/`](docs/azure/00-overview.md).

**Must use StorageV2** (general-purpose v2), not a Blob-only account.

## Project layout

```
AbcRetail/          MVC web app (.NET 10)
docs/azure/         How to create Azure resources
```

## Deploy

See [`docs/azure/07-app-service-deploy.md`](docs/azure/07-app-service-deploy.md).

App Setting name: `AzureStorage__ConnectionString`

## Submission

See [`docs/azure/09-screenshot-checklist.md`](docs/azure/09-screenshot-checklist.md) for rubric screenshots (≥5 records per service).

## Note on target framework

Scaffolded with the installed SDK templates as **net10.0**. If your campus App Service only offers .NET 8, change `<TargetFramework>` in `AbcRetail.csproj` to `net8.0` (requires the .NET 8 targeting pack) and select .NET 8 in App Service.
