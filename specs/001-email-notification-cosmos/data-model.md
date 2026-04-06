# Data Model: ACSdemo Refactoring — Email Notification Controller

**Feature**: `001-email-notification-cosmos`  
**Date**: 2026-04-06  
**Input**: spec.md, research.md

---

## Entities

### EmailDeliveryReport

The primary domain entity and Cosmos DB document. Represents the complete lifecycle of a
single email send attempt, identified by its ACS-assigned message ID.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Id` | `string` | ACS `messageId` | Cosmos DB document `id`; GUID string |
| `RecipientAddress` | `string` | ACS `recipient` | **Partition key** (`/recipientAddress`) |
| `SenderAddress` | `string` | ACS `sender` | Sender email address |
| `CurrentStatus` | `string` | Most recent ACS `status` | Open string; not validated against enum |
| `LastEventTimestamp` | `DateTimeOffset` | Most recent `deliveryAttemptTimeStamp` | Updated on each event |
| `StatusHistory` | `List<DeliveryStatusEntry>` | Accumulated per-event | Ordered; earliest entry first |

**Validation rules** (enforced via `Guard.Against.Null()` in constructor):
- `Id` must not be null or empty.
- `RecipientAddress` must not be null or empty.
- `SenderAddress` must not be null or empty.
- `StatusHistory` must not be null (may be empty on construction).

**State transitions**: A report is created when the first event for a `messageId` arrives.
Subsequent events append to `StatusHistory` and update `CurrentStatus` and `LastEventTimestamp`.
No record is ever deleted by application logic.

---

### DeliveryStatusEntry

An embedded value object stored as an array element within `EmailDeliveryReport.StatusHistory`.
Represents a single delivery status reported by ACS at a point in time.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Status` | `string` | ACS `status` | Same open-string semantics as `CurrentStatus` |
| `StatusMessage` | `string?` | ACS `deliveryStatusDetails.statusMessage` | Nullable; human-readable detail |
| `Timestamp` | `DateTimeOffset` | ACS `deliveryAttemptTimeStamp` | **Deduplication key** for idempotency |

**Idempotency rule**: Before appending a new `DeliveryStatusEntry`, the handler checks
whether any existing entry in `StatusHistory` has the exact same `Timestamp`. If a match is
found, the event is a duplicate and the append is skipped (document remains unchanged).

---

## Cosmos DB Container Configuration

| Property | Value |
|---|---|
| Container name | `EmailDeliveryReports` |
| Partition key | `/recipientAddress` |
| Document `id` | ACS `messageId` (GUID string) |
| Database name | `acsdemo` |
| Consistency level | Session (default) |
| Indexing | Default policy; add composite index on `/recipientAddress + /lastEventTimestamp DESC` for FR-006 sort query |

---

## Incoming Event DTO (Web Layer)

The following DTO lives in `Web/Models/` and is deserialized from the Dapr CloudEvents envelope.
It is NOT the domain entity — it represents the raw ACS event payload.

### EmailDeliveryReportReceivedEvent

| Field | Type | JSON Property | Notes |
|---|---|---|---|
| `Sender` | `string` | `sender` | |
| `Recipient` | `string` | `recipient` | |
| `MessageId` | `string` | `messageId` | |
| `Status` | `string` | `status` | |
| `DeliveryStatusDetails` | `DeliveryStatusDetails?` | `deliveryStatusDetails` | |
| `DeliveryAttemptTimeStamp` | `DateTimeOffset` | `deliveryAttemptTimeStamp` | Note capital S in TimeStamp |

### DeliveryStatusDetails

| Field | Type | JSON Property |
|---|---|---|
| `StatusMessage` | `string?` | `statusMessage` |

---

## Cosmos DB Document Example

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "recipientAddress": "receiver@azure.com",
  "senderAddress": "sender@azure.com",
  "currentStatus": "Delivered",
  "lastEventTimestamp": "2020-09-18T00:22:20.2855749+00:00",
  "statusHistory": [
    {
      "status": "Delivered",
      "statusMessage": "Status Message",
      "timestamp": "2020-09-18T00:22:20.2855749+00:00"
    }
  ]
}
```

If the same message subsequently bounces (edge case scenario), the document becomes:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "recipientAddress": "receiver@azure.com",
  "senderAddress": "sender@azure.com",
  "currentStatus": "Bounced",
  "lastEventTimestamp": "2020-09-18T01:05:10.0000000+00:00",
  "statusHistory": [
    {
      "status": "Delivered",
      "statusMessage": "Status Message",
      "timestamp": "2020-09-18T00:22:20.2855749+00:00"
    },
    {
      "status": "Bounced",
      "statusMessage": "Hard bounce",
      "timestamp": "2020-09-18T01:05:10.0000000+00:00"
    }
  ]
}
```

---

## Application Layer — CQRS Types

### Commands

#### SaveEmailDeliveryReportCommand

Carries the incoming event data from the Web layer to the Application handler.
Co-located in `Application/Commands/SaveEmailDeliveryReport.cs` with its handler and validator.

| Property | Type | Notes |
|---|---|---|
| `Event` | `EmailDeliveryReportReceivedEvent` | Full raw event payload |

**Handler output**: `Unit` (void — write operation)  
**Handler logic**: Read existing document → merge → upsert. See research.md §4 for idempotency pattern.

---

### Queries

Two queries are co-located in `Application/Queries/GetEmailDeliveryReports.cs`.

#### GetEmailDeliveryReportByIdQuery

| Property | Type | Notes |
|---|---|---|
| `MessageId` | `string` | Cosmos DB document `id` |

**Handler output**: `EmailDeliveryReport?`  
**Handler logic**: Point read by `id`. Requires a cross-partition query or a known recipient address. Since `id` is globally unique, use a cross-partition query with `WHERE c.id = @messageId` (small cost; acceptable for P2 read path).

#### GetEmailDeliveryReportsByRecipientQuery

| Property | Type | Notes |
|---|---|---|
| `RecipientAddress` | `string` | Partition key value |
| `Skip` | `int` | Default: 0 |
| `Take` | `int` | Default: 20 (max 100) |

**Handler output**: `IReadOnlyList<EmailDeliveryReport>`  
**Handler logic**: In-partition query on `/recipientAddress`, ordered by `lastEventTimestamp DESC`, offset/limit applied. Uses composite index.
