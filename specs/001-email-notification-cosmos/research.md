# Research: ACSdemo Refactoring — Email Notification Controller

**Feature**: `001-email-notification-cosmos`  
**Date**: 2026-04-06  
**Phase**: 0 — Pre-design research

---

## 1. ACS EmailDeliveryReportReceived Event Schema

**Source**: [Azure Event Grid — Communication Services Email Events](https://learn.microsoft.com/en-us/azure/event-grid/communication-services-email-events)

### Decision
Use the authoritative ACS schema fields directly as the C# event DTO property names (camelCase → PascalCase in C#).

### Canonical Payload

```json
[{
  "id": "00000000-0000-0000-0000-000000000000",
  "topic": "/subscriptions/{sub}/resourceGroups/{rg}/providers/microsoft.communication/communicationservices/{name}",
  "subject": "sender/senderid@azure.com/message/00000000-0000-0000-0000-000000000000",
  "data": {
    "sender": "senderid@azure.com",
    "recipient": "receiver@azure.com",
    "messageId": "00000000-0000-0000-0000-000000000000",
    "status": "Delivered",
    "deliveryStatusDetails": {
      "statusMessage": "Status Message"
    },
    "deliveryAttemptTimeStamp": "2020-09-18T00:22:20.2855749+00:00"
  },
  "eventType": "Microsoft.Communication.EmailDeliveryReportReceived",
  "dataVersion": "1.0",
  "metadataVersion": "1",
  "eventTime": "2020-09-18T00:22:20.822Z"
}]
```

### ACS-Defined Status Values

| Value | Meaning |
|---|---|
| `Delivered` | Successfully handed to recipient MTA |
| `Suppressed` | Recipient previously hard-bounced; temporarily suppressed |
| `Bounced` | Hard bounce — bad address or invalid domain |
| `Quarantined` | Identified as spam/bulk/phishing |
| `FilteredSpam` | Rejected/blocked as spam (not quarantined) |
| `Expanded` | Distribution group expanded before delivery |
| `Failed` | Message not delivered |

### Rationale
Status is treated as an open string (spec clarification 2026-04-05). The table above is informational — no closed-enum validation is applied.

### Key Naming Note
The timestamp field is `deliveryAttemptTimeStamp` (capital **S** in TimeStamp) — this is the exact casing in the official schema. C# DTO property must align to ensure JSON deserialization under Dapr's CloudEvents envelope works correctly.

---

## 2. HotChocolate v15 with ASP.NET Core

**Source**: [chillicream.com/docs/hotchocolate/v15](https://chillicream.com/docs/hotchocolate/v15), NuGet

### Decision
Use `HotChocolate.AspNetCore` v15.x (current stable: 15.1.13). Add `HotChocolate.ApolloFederation` for Apollo Federation support.

### Required NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `HotChocolate.AspNetCore` | 15.x | GraphQL server + Nitro IDE middleware |
| `HotChocolate.ApolloFederation` | 15.x | Apollo Federation annotations |

### Program.cs Registration Pattern

```csharp
// Registration
builder.Services
    .AddGraphQLServer()
    .AddQueryType<EmailNotificationQueries>()
    .AddApolloFederation();

// Middleware
app.MapGraphQL(); // exposes /graphql
```

### Query Type Pattern (Annotation-Based)

```csharp
[QueryType]
public class EmailNotificationQueries : IGraphQLQuery
{
    public async Task<EmailDeliveryReport?> GetEmailDeliveryReportAsync(
        string messageId,
        [Service] IMediator mediator,
        CancellationToken ct)
        => await mediator.Send(new GetEmailDeliveryReportByIdQuery(messageId), ct);

    [UsePaging(DefaultPageSize = 20)]
    public async Task<IEnumerable<EmailDeliveryReport>> GetEmailDeliveryReportsByRecipientAsync(
        string recipientAddress,
        [Service] IMediator mediator,
        CancellationToken ct)
        => await mediator.Send(new GetEmailDeliveryReportsByRecipientQuery(recipientAddress), ct);
}
```

### Alternatives Considered
- **Hot Chocolate Fusion (v15)**: ChilliCream's own federation gateway — not Apollo-compatible. Rejected: constitution mandates Apollo Federation.
- **Hot Chocolate v14**: Previous stable version — rejected: constitution locks to v15.x.

---

## 3. MediatR 12.x Registration

**Source**: [MediatR GitHub Wiki](https://github.com/jbogard/MediatR/wiki)

### Decision
Use `MediatR` v12.x. The extension package `MediatR.Extensions.Microsoft.DependencyInjection` was merged into the main `MediatR` package as of v12; it is no longer a separate NuGet package.

### Required NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `MediatR` | 12.x | CQRS pipeline + DI integration (merged extensions) |

### Registration Pattern

```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
```

### FluentValidation Pipeline Behaviour

To wire FluentValidation into the MediatR pipeline, add a `ValidationBehavior<TRequest, TResponse>` as a pipeline behaviour:

```csharp
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

`ValidationBehavior` is a project-level class in `Application/Behaviours/` that iterates all registered `IValidator<TRequest>` instances before the handler executes.

### Alternatives Considered
- Calling FluentValidation in the controller: rejected — violates Principle I (all validation via MediatR pipeline).

---

## 4. Azure Cosmos DB — Idempotent Upsert with StatusHistory Append

**Source**: Azure Cosmos DB SDK v3 (`Microsoft.Azure.Cosmos`)

### Decision
Use a **read-modify-write** (optimistic check) pattern:
1. Attempt to read the document by `messageId` (logical document `id`).
2. **Not found** → create a new document with `statusHistory` containing the single current entry.
3. **Found** → check if `statusHistory` already contains an entry with `deliveryAttemptTimeStamp` equal to the incoming event's timestamp. If yes, skip (already processed — idempotent). If no, append new entry and upsert.

### SDK Method

```csharp
// Create or replace entire document (includes merged statusHistory):
await container.UpsertItemAsync(document, new PartitionKey(document.RecipientAddress), cancellationToken: ct);
```

`UpsertItemAsync` is the correct method: it creates the document if `id` does not exist, or replaces it entirely if it does. Since we read-modify-write, we always send the complete merged document.

### Alternative: Patch API
`container.PatchItemAsync` with `PatchOperation.Add("/statusHistory/-", entry)` avoids a read, but does not natively support the duplicate-check requirement (idempotency). The read-modify-write pattern is simpler and ensures all invariants are met in one place.

### Rationale
- **YAGNI**: read-modify-write is conceptually simple and meets the spec without extra infrastructure.
- **Idempotency** (FR-004, SC-003): timestamp-based deduplication within `statusHistory` prevents duplicate entries from re-delivered messages.

---

## 5. Dapr Pub/Sub Subscription Pattern

**Source**: [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk), existing codebase patterns

### Decision
Use `[Topic("pubsub", "EmailDeliveryReportReceived")]` attribute on the controller action. The controller only dispatches to MediatR — no business logic.

### Registration Requirements

```csharp
// Program.cs — already present in current codebase:
builder.Services.AddControllers().AddDapr();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();
```

### Controller Pattern

```csharp
[HttpPost("email-delivery-report")]
[Topic("pubsub", "EmailDeliveryReportReceived")]
public async Task<IActionResult> HandleEmailDeliveryReport(
    [FromBody] EmailDeliveryReportReceivedEvent evt,
    CancellationToken ct)
{
    await _mediator.Send(new SaveEmailDeliveryReportCommand(evt), ct);
    return Ok();
}
```

Returning a non-2xx status causes Dapr to withhold the acknowledgement and retry. The handler must propagate exceptions so the controller returns 500 on failure (FR-009).

### Local vs Production Pub/Sub Component
- **Local** (`dapr/components/pubsub.yaml`): Redis (for local Docker Compose dev).
- **Azure** (Container Apps Dapr component): `pubsub.azure.servicebus.queues` — already configured in `deploy/containerapp.bicep`.

The topic name `EmailDeliveryReportReceived` must be created as a subscription on the Azure Service Bus namespace that the Dapr component references. This is an infrastructure concern captured in the Bicep plan.

### Alternatives Considered
- **Direct HTTP webhook**: Rejected — violates constitution Principle V (Dapr pub/sub only).
- **Azure Function trigger**: Rejected — out of scope for this single-project microservice.
