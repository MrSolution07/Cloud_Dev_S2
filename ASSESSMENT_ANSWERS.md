# CLDV7112 Project 2 — Assessment answers

**Student number:** ________________  
**Full name:** Christian Bulabula Emungu  
**Module code:** CLDV7112  
**Assessment:** Project 2  
**GitHub repository:** https://github.com/MrSolution07/Cloud_Dev_S2  
**Deployed web application URL:** ________________ (App Service, similar to `https://{student_number}.azurewebsites.net`)  
**Azure Function App URL:** ________________ (for example `https://{student_number}-fn.azurewebsites.net`)

Copy this file into Microsoft Word as `{StudentNumber}_CLDV7112_Project2` before LMS upload. Begin each numbered section on a new page in Word (Layout → Breaks → Page). Paste screenshots under the matching headings. Do not zip the repository into the LMS; the GitHub link is the source-code submission.

---

<div style="page-break-before: always;"></div>

## A. Integrating Functions to build a robust application architecture

ABC Retail’s Phase 1 MVC application already used Azure Table Storage, Blob Storage, Queue Storage, and Azure Files from the web app process. Project 2 keeps that storefront and admin UI, and adds an isolated Azure Functions worker (`AbcRetail.Functions`, .NET 8) so the four storage services are also called from functions. The MVC site integrates those functions through `IFunctionGateway` (`AbcRetail/Services/FunctionGateway.cs`). When `AzureFunctions:BaseUrl` is set, writes go over HTTP to the Function App (`x-functions-key`). When the base URL is empty, the same gateway falls back to the Phase 1 Storage SDK so `dotnet run` still works for local testing.

This split improves cost and scale in the way Microsoft describes for Azure Functions: the web app stays busy with HTTP pages, while storage I/O can run as separately billed, event-ready functions (Microsoft, 2024a). The functions use the **same** storage account and resource names as Phase 1 (`Customers`, `Products`, `Orders`, `CartItems`, `product-images`, `order-processing`, `inventory-management`, `applogs/logs`).

### A1. Function that stores information in Azure Tables — `StoreTable` (20)

**Code:** `AbcRetail.Functions/StoreTableFunction.cs`  
**Route:** `POST /api/StoreTable`  
**Auth:** Function key (`AuthorizationLevel.Function`) plus `x-functions-key`

`StoreTable` upserts, deletes, or adjusts stock in Azure Table Storage. Tables used:

- `Customers` — registration, profile, admin user create/delete  
- `Products` — create, edit, delete, stock decrement after payment  
- `Orders` — checkout and payment status  
- `CartItems` — durable cart lines (partition = customer email)

The MVC product-create, register, checkout, and cart actions call `IFunctionGateway.UpsertAsync` / `DeleteAsync` / `DecrementStockAsync`, which POST JSON `{ table, operation, partitionKey, rowKey, properties }` to this function. Stock updates retry on HTTP 412 (ETag conflict) in `StorageBridge.MutateProductAsync`.

**Screenshots required:** Function App blade showing `StoreTable`; the C# method; Azure Storage Explorer or portal Tables with new `Orders` / `CartItems` rows after a checkout.

### A2. Function that writes to Azure Blob Storage — `WriteBlob` (20)

**Code:** `AbcRetail.Functions/WriteBlobFunction.cs`  
**Route:** `POST /api/WriteBlob`

Admin product create/edit sends image bytes as Base64 (max 1 MB). The function writes to container `product-images` and returns `blobName`. Operation `delete` removes blobs when an admin deletes a product, so images are not left orphaned. Public blob access is still used so the shop can render `<img>` tags, matching Phase 1.

**Screenshots required:** Function App showing `WriteBlob`; code; container `product-images` with blobs; shop page showing those images.

### A3. Function that reads from and writes to Azure Queues — `QueueTransaction` (20)

**Code:** `AbcRetail.Functions/QueueTransactionFunction.cs`  
**Route:** `POST /api/QueueTransaction` (write) and `GET /api/QueueTransaction?queue=...` (read/peek)

One function covers both directions required by the rubric. After a **successful** simulated payment, the MVC checkout posts one `order-processing` message per line in the Phase 1 pipe format, with quantity and order id appended so existing admin parsers still read `parts[1]` (product id), `parts[3]` (image name) and `parts[4]` (email):

`Processing order|{productId}|{productName}|{image}|{email}|{utc}|{qty}|{orderId}`

Inventory messages use `inventory-management`. GET peeks up to 32 messages so the admin Orders page can show queue contents through the function. Admin **Process oldest order** still dequeues with the Phase 1 SDK so messages remain visible for portal screenshots until that button is used. There is no QueueTrigger, which would empty the queue before screenshots.

Failed or cancelled payments do **not** write queue messages and do **not** decrement stock.

**Screenshots required:** Function App showing `QueueTransaction`; code; a message sitting in `order-processing`; admin Orders peek list.

### A4. Function that sends a file to Azure Files — `WriteFile` (20)

**Code:** `AbcRetail.Functions/WriteFileFunction.cs`  
**Route:** `POST /api/WriteFile`

Writes `{fileName}` under share `applogs`, directory `logs`. Used by:

- Admin Logs page (“Write log entry”)  
- Payment success (`order-{orderId}.log`)  
- Payment failure (`payment-failed-{orderId}.log`) — no card numbers

Phase 1 activity traces (`WriteActivityAsync`) still append daily `activity-yyyy-MM-dd.log` files from the web app so existing log evidence remains.

**Screenshots required:** Function App showing `WriteFile`; code; file listed in Azure Files; Logs page in the web app.

### How the web app calls the functions

Configure user secrets or App Settings:

- `AzureFunctions:BaseUrl` = `http://localhost:7071/api` (local) or `https://{function-app}.azurewebsites.net/api`  
- `AzureFunctions:Key` = function key (`FUNCTIONS_ACCESS_KEY` on the Function App)

Local demo without functions: leave `BaseUrl` empty; the gateway uses the Storage SDK. For Project 2 marking, run **both** `func start` and `dotnet run`, or deploy the Function App and set the App Settings.

---

<div style="page-break-before: always;"></div>

## B. Using services for improving the customer experience (20)

The brief asks how **Azure Event Hubs** and **Azure Event Bus** could add value for ABC Retail customers. Microsoft’s product name for the second service is **Azure Service Bus**; the discussion below uses that official name and treats “Event Bus” as the brief’s label for it (Microsoft, 2024b).

### B1. Azure Event Hubs

**Description of service**  
Azure Event Hubs is a managed ingestion service for high-volume telemetry and clickstream data. Producers send events into a namespace; consumer groups read those events independently, including into analytics pipelines (Microsoft, 2024c).

**Mechanism**  
ABC Retail’s storefront already records discrete actions (add to cart, payment outcome, stock change). Those same events could be published to Event Hubs as JSON (product id, customer hash, timestamp, outcome) without blocking checkout. Stream processors or Azure Stream Analytics could then count popular products during a peak such as Black Friday, which is the scaling problem described in the project background.

**How it adds value to end users**  
Shoppers would see faster personalisation: recently viewed or frequently bought items could be recommended from near-real-time counts rather than overnight batch reports. During peak season, operations could spot a failing payment spike and show a clear “try again” message instead of a generic error. The customer benefit is relevance and fewer failed checkouts, not a change to how cards are entered.

### B2. Azure Event Bus (Azure Service Bus)

**Description of service**  
Azure Service Bus is a fully managed enterprise message broker. It supports queues (one consumer) and topics/subscriptions (many consumers), with features such as sessions, dead-lettering, and scheduled delivery (Microsoft, 2024b).

**Mechanism**  
Phase 1 and Project 2 already use **Azure Queue Storage** for `order-processing` and `inventory-management`. Service Bus would sit one level up for commands that must not be lost and may need multiple subscribers: for example a `OrderPaid` topic with subscriptions for warehouse picking, email notification, and loyalty points. Peek-lock and dead-letter queues would replace “message disappeared after dequeue” behaviour that Queue Storage exhibits when a worker crashes mid-process.

**How it adds value to end users**  
Reliable fan-out means a paid order still generates a confirmation email and a pick ticket even if one downstream worker is down. Scheduled messages could drive “your order has shipped” notices. Dead-letter investigation reduces silent lost orders, which is the complaint pattern in the ABC Retail scenario. For this coursework the Storage queues remain in place so Project 1 evidence still holds; Service Bus is the production-shaped next step, not a replacement shipped in this repository.

---

<div style="page-break-before: always;"></div>

## Extra implementation (not separately marked)

The storefront is a complete customer journey on the same C# / Azure stack: register and login (cookie auth, `PasswordHasher`), search and categories, table-backed cart with quantity and stock checks, checkout (delivery → summary → simulated payment → confirmation), and My Orders. Payment is a **simulation only**. Test PAN `4242424242424242` succeeds; `4000000000000002` (and other non-4242 numbers) fail; Cancel leaves the order cancelled. Card numbers are never stored; only last four digits and a `sim_` reference are saved. Server-side prices come from Tables. Admin product edit, order status, customer search, and blob-delete-on-product-delete are included. Default admin remains `admin@abcretail.local` (password in README only, not on the login page).

---

<div style="page-break-before: always;"></div>

## Deployment and testing

The MVC app deploys to Azure App Service as in Project 1 (`docs/azure/07-app-service-deploy.md`). The Function App deploys separately (`docs/azure/10-function-app.md`). Both use `AzureStorage__ConnectionString` against the existing StorageV2 account. Screenshot list: `docs/azure/11-project2-screenshot-checklist.md`.

Local:

```bash
# Terminal 1 — Functions (copy local.settings.json.example first and paste the storage connection string)
cd AbcRetail.Functions
func start

# Terminal 2 — MVC
cd AbcRetail
dotnet user-secrets set "AzureStorage:ConnectionString" "<storage connection string>"
dotnet user-secrets set "AzureFunctions:BaseUrl" "http://localhost:7071/api"
dotnet user-secrets set "AzureFunctions:Key" "dev-local-key"
dotnet run
```

---

<div style="page-break-before: always;"></div>

## Reference list

Microsoft. 2024a. *Azure Functions overview*. [Online]. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/functions-overview [Accessed 16 September 2026].

Microsoft. 2024b. *Azure Service Bus messaging overview*. [Online]. Available at: https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-messaging-overview [Accessed 16 September 2026].

Microsoft. 2024c. *What is Azure Event Hubs?* [Online]. Available at: https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-about [Accessed 16 September 2026].

Microsoft. 2024d. *Azure Table Storage overview*. [Online]. Available at: https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview [Accessed 16 September 2026].

Microsoft. 2024e. *Introduction to Azure Blob Storage*. [Online]. Available at: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-overview [Accessed 16 September 2026].

Microsoft. 2024f. *What is Azure Queue Storage?* [Online]. Available at: https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction [Accessed 16 September 2026].

Microsoft. 2024g. *What is Azure Files?* [Online]. Available at: https://learn.microsoft.com/en-us/azure/storage/files/storage-files-introduction [Accessed 16 September 2026].

Microsoft. 2025. *Guide for running C# Azure Functions in an isolated worker process*. [Online]. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide [Accessed 16 September 2026].
