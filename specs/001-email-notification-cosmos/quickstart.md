# Quickstart: ACSdemo — Email Notification Controller

**Feature**: `001-email-notification-cosmos`  
**Date**: 2026-04-06

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | `dotnet --version` |
| Docker Desktop | Latest | Required for Docker Compose + Cosmos DB emulator |
| Dapr CLI | 1.x | `dapr --version`; initialised via `dapr init` |
| Azure CLI | Latest | For Azure deployment only |

---

## Local Development Setup

### 1. Configure User Secrets (local only — never commit)

```powershell
cd src/ACSdemo.Api

# Cosmos DB Emulator (default TLS endpoint + well-known key)
dotnet user-secrets set "CosmosDb:AccountEndpoint" "https://localhost:8081"
dotnet user-secrets set "CosmosDb:AccountKey" "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
dotnet user-secrets set "CosmosDb:DatabaseName" "acsdemo"
dotnet user-secrets set "CosmosDb:ContainerName" "EmailDeliveryReports"
```

> The Cosmos DB Emulator key above is the well-known public emulator master key.  
> ⚠️ Never use this key in any non-local environment.

### 2. Start Local Infrastructure

```bash
# From repo root — starts Redis (for Dapr pub/sub) and the Cosmos DB Emulator
docker-compose up -d
```

The Cosmos DB Emulator UI is available at `https://localhost:8081/_explorer/index.html`.  
Accept the self-signed certificate on first visit.

### 3. Run the API with Dapr

```bash
# From repo root
dapr run \
  --app-id acsdemo-api \
  --app-port 5000 \
  --components-path ./dapr/components \
  -- dotnet run --project src/ACSdemo.Api
```

The API starts at `http://localhost:5000`.  
The GraphQL IDE (Nitro) is available at `http://localhost:5000/graphql`.

---

## Verifying the Dapr Subscription

### Check subscription registration

```bash
curl http://localhost:3500/dapr/subscribe
```

Expected response includes:

```json
[
  {
    "pubsubname": "pubsub",
    "topic": "EmailDeliveryReportReceived",
    "route": "/api/emailnotification/email-delivery-report"
  }
]
```

### Publish a test event

```bash
dapr publish \
  --publish-app-id acsdemo-api \
  --pubsub pubsub \
  --topic EmailDeliveryReportReceived \
  --data '{
    "sender": "sender@example.com",
    "recipient": "recipient@example.com",
    "messageId": "test-message-001",
    "status": "Delivered",
    "deliveryStatusDetails": { "statusMessage": "OK" },
    "deliveryAttemptTimeStamp": "2026-04-06T10:00:00+00:00"
  }'
```

The command should return successfully. Check Cosmos DB emulator for the persisted document.

---

## Verifying GraphQL Queries

Open `http://localhost:5000/graphql` in a browser and run the following queries in Nitro.

### Query by message ID

```graphql
query {
  emailDeliveryReport(messageId: "test-message-001") {
    id
    recipientAddress
    senderAddress
    currentStatus
    lastEventTimestamp
    statusHistory {
      status
      statusMessage
      timestamp
    }
  }
}
```

### Query by recipient (paginated)

```graphql
query {
  emailDeliveryReportsByRecipient(
    recipientAddress: "recipient@example.com"
    skip: 0
    take: 20
  ) {
    totalCount
    hasNextPage
    items {
      id
      currentStatus
      lastEventTimestamp
    }
  }
}
```

---

## Project Structure Reference

```
src/ACSdemo.Api/
├── Domain/
│   ├── Entities/
│   │   └── EmailDeliveryReport.cs        # Entity + DeliveryStatusEntry
│   └── Configuration/
│       └── DomainServiceExtensions.cs
├── Application/
│   ├── Behaviours/
│   │   └── ValidationBehavior.cs         # MediatR FluentValidation pipeline
│   ├── Commands/
│   │   └── SaveEmailDeliveryReport.cs    # Command + Handler + Validator (co-located)
│   ├── Queries/
│   │   └── GetEmailDeliveryReports.cs    # Both queries + handlers (co-located)
│   ├── Ports/
│   │   └── IEmailDeliveryReportRepository.cs
│   └── Configuration/
│       └── ApplicationServiceExtensions.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── CosmosEmailDeliveryReportRepository.cs
│   └── Configuration/
│       └── InfrastructureServiceExtensions.cs
├── Web/
│   ├── Controllers/
│   │   └── EmailNotificationController.cs
│   ├── GraphQL/
│   │   ├── Interfaces/
│   │   │   ├── IGraphQLQuery.cs
│   │   │   ├── IGraphQLMutation.cs
│   │   │   └── IGraphQLType.cs
│   │   └── EmailNotificationQueries.cs
│   ├── Models/
│   │   └── EmailDeliveryReportReceivedEvent.cs
│   └── Configuration/
│       └── WebServiceExtensions.cs
└── Program.cs
```

---

## Common Problems

| Problem | Solution |
|---|---|
| Cosmos DB emulator certificate error | Accept self-signed cert in browser at `https://localhost:8081` before starting the app |
| Dapr sidecar not starting | Run `dapr init` to initialise local Dapr runtime |
| GraphQL endpoint returns 404 | Confirm `app.MapGraphQL()` is called in `Program.cs` after `app.UseRouting()` |
| Event not received by subscription | Confirm `app.UseCloudEvents()` and `app.MapSubscribeHandler()` are registered before `app.MapControllers()` |
