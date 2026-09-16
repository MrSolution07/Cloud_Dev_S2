# ABC Retail — CLDV7112 Project 1 + Project 2

**Author:** Christian Bulabula Emungu ([@MrSolution07](https://github.com/MrSolution07))

ASP.NET Core MVC app for **ABC Retail** using Azure Storage, plus four Azure Functions (Project 2).

| Service | Feature |
| --- | --- |
| Azure Tables | Customers, Products, Orders, CartItems |
| Azure Blob Storage | Product images (`product-images`) |
| Azure Queues | `order-processing` + `inventory-management` |
| Azure Files | Log files (`applogs` / `logs`) |
| Azure Functions | `StoreTable`, `WriteBlob`, `QueueTransaction`, `WriteFile` |

UI: liquid-glass storefront. Prices in **ZAR (R)**. Payment is a **simulation** (no real card processing).

## Accounts & roles

- **Customer** — `/Account/Register`. Shop, cart, checkout, simulated payment, My orders, profile.
- **Admin** — products (create/edit/delete, stock), customers, orders + queues, logs.

Seeded admin (change before sharing):

```
Email:    admin@abcretail.local
Password: Admin@12345
```

## Quick start (MVC only)

```bash
cd AbcRetail
dotnet restore
dotnet user-secrets set "AzureStorage:ConnectionString" "<your-storage-connection-string>"
dotnet run
```

Cart, checkout, and admin still work: `IFunctionGateway` uses the Storage SDK when `AzureFunctions:BaseUrl` is empty.

## Quick start (MVC + Functions — Project 2)

```bash
cp AbcRetail.Functions/local.settings.json.example AbcRetail.Functions/local.settings.json
# paste the storage connection string into AzureWebJobsStorage and AzureStorage__ConnectionString

cd AbcRetail.Functions && func start
```

```bash
cd AbcRetail
dotnet user-secrets set "AzureFunctions:BaseUrl" "http://localhost:7071/api"
dotnet user-secrets set "AzureFunctions:Key" "dev-local-key"
dotnet run
```

Simulated cards: success `4242424242424242` · decline `4000000000000002`. Never enter a real PAN.

## Azure setup

Guides in [`docs/azure/`](docs/azure/00-overview.md). **StorageV2** required.

Project 2 Function App: [`docs/azure/10-function-app.md`](docs/azure/10-function-app.md).

## Project layout

```
AbcRetail/              MVC web app (.NET 10)
AbcRetail.Functions/    Isolated worker (.NET 8) — four HTTP functions
AbcRetail/wwwroot/images/catalog/  Shop JPEGs uploaded to Blob Storage
img/                    Source PNGs (AirPods, Z Fold, Sony camera)
docs/azure/             Portal + CLI guides
ASSESSMENT_ANSWERS.md   Project 2 written answers (IIE Harvard)
```

## Deploy

- Web App: [`docs/azure/07-app-service-deploy.md`](docs/azure/07-app-service-deploy.md) — `AzureStorage__ConnectionString`
- Function App: [`docs/azure/10-function-app.md`](docs/azure/10-function-app.md)
- Web App also needs `AzureFunctions__BaseUrl` and `AzureFunctions__Key` after the Function App is live

## Submission

- Project 1 screenshots: [`docs/azure/09-screenshot-checklist.md`](docs/azure/09-screenshot-checklist.md)
- Project 2 screenshots: [`docs/azure/11-project2-screenshot-checklist.md`](docs/azure/11-project2-screenshot-checklist.md)
- Written answers: [`ASSESSMENT_ANSWERS.md`](ASSESSMENT_ANSWERS.md) → Word file `{StudentNumber}_CLDV7112_Project2`

## Note on target framework

MVC is **net10.0**. Functions are **net8.0 isolated** for campus Function App runtimes. If App Service has no .NET 10, retarget `AbcRetail.csproj` to `net8.0`.
