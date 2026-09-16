# 11 — Project 2 screenshot checklist (rubric)

Word doc name: `{StudentNumber}_CLDV7112_Project2`

Include: student number (blank until you fill it), name Christian Bulabula Emungu, module CLDV7112, GitHub URL, live App Service URL, Function App URL.

Disable nothing on the queues until the queue screenshots are done. Paid checkout writes messages; admin **Process oldest order** removes them.

## StoreTable (20)

- [ ] Portal: Function App list includes `StoreTable`
- [ ] Code: `StoreTableFunction.cs` (IDE or Code + Test)
- [ ] Portal/Storage Explorer: `Orders` and/or `CartItems` table with entities after a customer checkout

## WriteBlob (20)

- [ ] Portal: `WriteBlob` on the Function App
- [ ] Code: `WriteBlobFunction.cs`
- [ ] Portal: container `product-images` showing blobs
- [ ] App: product images on Shop / Details

## QueueTransaction (20)

- [ ] Portal: `QueueTransaction` on the Function App
- [ ] Code: `QueueTransactionFunction.cs` (GET read + POST write)
- [ ] Portal: message in `order-processing` (raw text includes product name and imageName)
- [ ] App: Admin → Orders peek list shows the same message

## WriteFile (20)

- [ ] Portal: `WriteFile` on the Function App
- [ ] Code: `WriteFileFunction.cs`
- [ ] Portal: share `applogs` / `logs` contains `order-{id}.log` or a manual log from Admin → Logs
- [ ] App: Logs page lists the filename

## Event Hubs / Event Bus write-up (20)

- [ ] Section B in the Word document: Description, Mechanism, How it adds value — for both services
- [ ] Harvard in-text citations and reference list present

## Deploy

- [ ] Portal: Web App Overview + URL
- [ ] Portal: Function App Overview + URL
- [ ] Browser: live shop + admin still work
- [ ] GitHub repository URL
