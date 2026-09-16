# 10 — Azure Function App (Project 2)

Official guides:

- [Azure Functions overview](https://learn.microsoft.com/en-us/azure/azure-functions/functions-overview)
- [Isolated worker .NET](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Create a function app in the portal](https://learn.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal)

The Functions project is `AbcRetail.Functions` (.NET 8 isolated, v4). It talks to the **same StorageV2 account** as the MVC app.

## Local

1. Install [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local).
2. Copy settings:

```bash
cp AbcRetail.Functions/local.settings.json.example AbcRetail.Functions/local.settings.json
```

3. Paste the storage connection string into **both** `AzureWebJobsStorage` and `AzureStorage__ConnectionString`. Set `FUNCTIONS_ACCESS_KEY` to `dev-local-key` (must match MVC `AzureFunctions:Key`).
4. Run:

```bash
cd AbcRetail.Functions
func start
```

Functions listen on `http://localhost:7071/api/{StoreTable|WriteBlob|QueueTransaction|WriteFile}`.

5. Point the MVC app at them:

```bash
cd AbcRetail
dotnet user-secrets set "AzureFunctions:BaseUrl" "http://localhost:7071/api"
dotnet user-secrets set "AzureFunctions:Key" "dev-local-key"
dotnet run
```

Leave `AzureFunctions:BaseUrl` empty to use the Phase 1 Storage SDK only (no Function host).

## Portal — create Function App

1. **Create a resource** → **Function App**.
2. Resource group: `rg-abc-retail-cldv7112` (same as Project 1).
3. Name: `{student_number}-fn` (must be globally unique).
4. Runtime: **.NET 8 Isolated**.
5. Hosting: Consumption (cost-effective for coursework).
6. Storage: **use the existing StorageV2 account** (not a second account).
7. Create.

## Application settings

| Name | Value |
|------|--------|
| `AzureStorage__ConnectionString` | same connection string as the Web App |
| `FUNCTIONS_ACCESS_KEY` | a random key you invent; also set on the Web App as `AzureFunctions__Key` |
| `AzureWebJobsStorage` | usually filled by the portal |

Web App extra settings:

| Name | Value |
|------|--------|
| `AzureFunctions__BaseUrl` | `https://{function-app}.azurewebsites.net/api` |
| `AzureFunctions__Key` | same as `FUNCTIONS_ACCESS_KEY` |

## Publish (CLI)

```bash
cd AbcRetail.Functions
func azure functionapp publish FUNCTION_APP_NAME
```

## Screenshot points

- Function App **Functions** list: `StoreTable`, `WriteBlob`, `QueueTransaction`, `WriteFile`
- One function’s **Code + Test** (or the C# file in the repo)
- After a paid checkout: message in queue `order-processing`; file under `applogs/logs`
