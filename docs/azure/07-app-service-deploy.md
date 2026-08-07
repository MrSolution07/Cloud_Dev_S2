# 07 — Deploy to Azure App Service

Official guide: [Deploy an ASP.NET Core app to App Service](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore)

This project targets **.NET 10**. In the App Service create blade, pick the newest available .NET stack (.NET 10 if listed; otherwise .NET 9/8 and retarget the csproj to match campus requirements).

## Portal — create Web App

1. **Create a resource** → **Web App**.
2. Resource group: `rg-abc-retail-cldv7112`
3. Name: `{student_number}` → URL becomes `https://{student_number}.azurewebsites.net`
4. Publish: **Code**
5. Runtime: **.NET 10** (or campus-required version)
6. OS: **Linux** (or Windows)
7. Plan: Free/Basic for coursework
8. Create.

## App Setting (connection string)

Configuration → Application settings → New application setting:

| Name | Value |
|------|-------|
| `AzureStorage__ConnectionString` | paste storage connection string |

Note the **double underscore** `__` — maps to `AzureStorage:ConnectionString` in ASP.NET Core.

Save → Restart the app.

## Deploy options

### A) Visual Studio (Windows campus)

Right-click project → **Publish** → Azure → App Service → select the web app → Publish.

### B) CLI from this repo

```bash
cd AbcRetail
dotnet publish -c Release -o ./publish
az webapp deploy \
  --resource-group rg-abc-retail-cldv7112 \
  --name STUDENT_NUMBER \
  --src-path ./publish \
  --type zip
```

Or:

```bash
az webapp up \
  --resource-group rg-abc-retail-cldv7112 \
  --name STUDENT_NUMBER \
  --runtime "DOTNETCORE:10.0"
```

## Smoke test online

Open the live URL → Customers / Products / Orders / Logs → confirm data still works with App Setting secrets.

## Screenshot points

- App Service Overview with URL
- Deployment success
- Browser showing the live glass UI
- Application setting name present (value blurred)
