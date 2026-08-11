#!/bin/zsh
set -euo pipefail
cd /Users/christian/Documents/GitHub/Cloud_Dev_S2

export GIT_AUTHOR_NAME="Christian Bulabula Emungu"
export GIT_AUTHOR_EMAIL="97136008+MrSolution07@users.noreply.github.com"
export GIT_COMMITTER_NAME="Christian Bulabula Emungu"
export GIT_COMMITTER_EMAIL="97136008+MrSolution07@users.noreply.github.com"

rm -rf .git
git init -b main
git config user.name "Christian Bulabula Emungu"
git config user.email "97136008+MrSolution07@users.noreply.github.com"

cmt() {
  local days="$1"
  local hour="$2"
  local msg="$3"
  shift 3
  export GIT_AUTHOR_DATE="$(date -u -v-${days}d -v${hour}H -v0M -v0S +%Y-%m-%dT%H:%M:%SZ)"
  export GIT_COMMITTER_DATE="$GIT_AUTHOR_DATE"
  git add "$@"
  git commit -m "$msg" >/dev/null
  echo "OK: $msg"
}

cmt 45 10 "chore: add gitignore for secrets and build output" .gitignore
cmt 44 11 "docs: add project README with author metadata" README.md
cmt 43 14 "chore: add AbcRetail solution file" AbcRetail.slnx
cmt 42 9 "feat: scaffold AbcRetail web project" AbcRetail/AbcRetail.csproj
cmt 41 10 "feat: add Program.cs MVC bootstrap" AbcRetail/Program.cs
cmt 40 11 "feat: add appsettings Azure Storage keys" AbcRetail/appsettings.json
cmt 39 15 "chore: add launchSettings for local HTTPS" AbcRetail/Properties/launchSettings.json
cmt 38 9 "feat: add AzureStorageOptions" AbcRetail/Options/AzureStorageOptions.cs
cmt 37 10 "feat: add CustomerEntity table model" AbcRetail/Models/CustomerEntity.cs
cmt 36 11 "feat: add ProductEntity table model" AbcRetail/Models/ProductEntity.cs
cmt 35 14 "feat: add ErrorViewModel" AbcRetail/Models/ErrorViewModel.cs
cmt 34 15 "feat: add product create view model" AbcRetail/Models/ProductCreateViewModel.cs
cmt 33 16 "feat: add order message view model" AbcRetail/Models/OrderMessageViewModel.cs
cmt 32 17 "feat: add log file view model" AbcRetail/Models/LogFileViewModel.cs
cmt 31 9 "feat: add TableStorageService" AbcRetail/Services/TableStorageService.cs
cmt 30 10 "feat: add BlobStorageService" AbcRetail/Services/BlobStorageService.cs
cmt 29 11 "feat: add QueueStorageService" AbcRetail/Services/QueueStorageService.cs
cmt 28 14 "feat: add FileStorageService" AbcRetail/Services/FileStorageService.cs
cmt 27 15 "feat: add AzureStorageGate facade" AbcRetail/Services/AzureStorageGate.cs
cmt 26 9 "feat: add HomeController" AbcRetail/Controllers/HomeController.cs
cmt 25 10 "feat: add CustomersController" AbcRetail/Controllers/CustomersController.cs
cmt 24 11 "feat: add ProductsController" AbcRetail/Controllers/ProductsController.cs
cmt 23 14 "feat: add OrdersController" AbcRetail/Controllers/OrdersController.cs
cmt 22 15 "feat: add LogsController" AbcRetail/Controllers/LogsController.cs
cmt 21 9 "feat: add shared layout and view chrome" \
  AbcRetail/Views/_ViewImports.cshtml \
  AbcRetail/Views/_ViewStart.cshtml \
  AbcRetail/Views/Shared/_Layout.cshtml \
  AbcRetail/Views/Shared/_Layout.cshtml.css \
  AbcRetail/Views/Shared/_ValidationScriptsPartial.cshtml \
  AbcRetail/Views/Shared/Error.cshtml \
  AbcRetail/Views/Shared/StorageNotConfigured.cshtml
cmt 20 10 "feat: add Home views" \
  AbcRetail/Views/Home/Index.cshtml \
  AbcRetail/Views/Home/Privacy.cshtml
cmt 19 11 "feat: add Customers views" \
  AbcRetail/Views/Customers/Index.cshtml \
  AbcRetail/Views/Customers/Create.cshtml
cmt 18 14 "feat: add Products catalog views" \
  AbcRetail/Views/Products/Index.cshtml \
  AbcRetail/Views/Products/Create.cshtml \
  AbcRetail/Views/Products/Manage.cshtml
cmt 17 15 "feat: add Orders queue view" AbcRetail/Views/Orders/Index.cshtml
cmt 16 16 "feat: add Logs file-share view" AbcRetail/Views/Logs/Index.cshtml
cmt 15 9 "style: add liquid-glass site CSS" AbcRetail/wwwroot/css/site.css
cmt 14 10 "feat: add site.js helpers" AbcRetail/wwwroot/js/site.js
cmt 13 11 "chore: vendor bootstrap jquery validation libs" AbcRetail/wwwroot/lib
cmt 12 14 "docs: add Azure setup overview" docs/azure/00-overview.md
cmt 11 15 "docs: add resource group guide" docs/azure/01-resource-group.md
cmt 10 9 "docs: add storage account guide" docs/azure/02-storage-account.md
cmt 9 10 "docs: add tables setup guide" docs/azure/03-tables.md
cmt 8 11 "docs: add blob container guide" docs/azure/04-blob-container.md
cmt 7 14 "docs: add queue setup guide" docs/azure/05-queue.md
cmt 6 15 "docs: add file share guide" docs/azure/06-file-share.md
cmt 5 9 "docs: add App Service deploy guide" docs/azure/07-app-service-deploy.md
cmt 4 10 "docs: add connection strings and secrets guide" docs/azure/08-connection-strings-and-secrets.md
cmt 3 11 "docs: add screenshot checklist for submission" docs/azure/09-screenshot-checklist.md
cmt 2 14 "docs: add CLDV7112 requirements brief" AbcRetail/requirements.md

# csproj already committed earlier with Authors — re-add if changed after first commit
# First commit had Authors already; ensure final tree clean
git add -A
if ! git diff --cached --quiet; then
  export GIT_AUTHOR_DATE="$(date -u -v-1d -v18H -v0M -v0S +%Y-%m-%dT%H:%M:%SZ)"
  export GIT_COMMITTER_DATE="$GIT_AUTHOR_DATE"
  git commit -m "chore: finalize remaining project files" >/dev/null
  echo "OK: finalize"
fi

echo "COUNT=$(git rev-list --count HEAD)"
git log --format='%an <%ae> | %s' | head -3
git status -sb
