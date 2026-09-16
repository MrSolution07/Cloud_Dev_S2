# 08 — Connection strings and secrets

Official guides:

- [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Configure app settings in App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-common)

## Local (User Secrets)

From the `AbcRetail` project folder:

```bash
dotnet user-secrets set "AzureStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
```

Verify:

```bash
dotnet user-secrets list
```

Restart `dotnet run`. Home page should show **Azure Storage ready**.

## App Service

Set:

```text
AzureStorage__ConnectionString = <connection string>
```

Optional overrides:

```text
AzureStorage__CustomersTable=Customers
AzureStorage__ProductsTable=Products
AzureStorage__BlobContainer=product-images
AzureStorage__QueueName=order-processing
AzureStorage__FileShare=applogs
AzureFunctions__BaseUrl=https://FUNCTION_APP.azurewebsites.net/api
AzureFunctions__Key=<same as FUNCTIONS_ACCESS_KEY>
```

## Never do this

- Commit connection strings to GitHub
- Paste keys into `appsettings.json`
- Zip secrets into the LMS submission

`appsettings.json` intentionally keeps an empty `ConnectionString`.
