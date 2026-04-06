# Dapr Pub/Sub Subscription Contract

**Feature**: `001-email-notification-cosmos`  
**Date**: 2026-04-06

---

## Endpoint

```
POST /api/emailnotification/email-delivery-report
```

This is a **Dapr pub/sub subscription endpoint**. It is NOT a public business API.
The Dapr sidecar calls it when a message arrives on the subscribed topic.

---

## Dapr Subscription Configuration

The endpoint is auto-registered via the `[Topic]` attribute. Dapr discovers it at:

```
GET /dapr/subscribe
```

Which returns:

```json
[
  {
    "pubsubname": "pubsub",
    "topic": "EmailDeliveryReportReceived",
    "route": "/api/emailnotification/email-delivery-report"
  }
]
```

---

## Request

**Method**: `POST`  
**Content-Type**: `application/cloudevents+json` (Dapr CloudEvents envelope)  
**Authentication**: None (sidecar-to-app communication on localhost)

### CloudEvents Envelope

```json
{
  "specversion": "1.0",
  "type": "com.dapr.event.sent",
  "source": "pubsub",
  "id": "<dapr-event-id>",
  "datacontenttype": "application/json",
  "pubsubname": "pubsub",
  "topic": "EmailDeliveryReportReceived",
  "data": {
    "sender": "senderid@azure.com",
    "recipient": "receiver@azure.com",
    "messageId": "00000000-0000-0000-0000-000000000000",
    "status": "Delivered",
    "deliveryStatusDetails": {
      "statusMessage": "Status Message"
    },
    "deliveryAttemptTimeStamp": "2020-09-18T00:22:20.2855749+00:00"
  }
}
```

### Data Payload Fields (ACS Schema)

| Field | Type | Required | Notes |
|---|---|---|---|
| `sender` | string | Yes | Sender email address |
| `recipient` | string | Yes | Recipient email address; used as partition key |
| `messageId` | string | Yes | ACS GUID; used as Cosmos DB document `id` |
| `status` | string | Yes | Delivery status (open string) |
| `deliveryStatusDetails.statusMessage` | string | No | Human-readable detail |
| `deliveryAttemptTimeStamp` | ISO 8601 datetime | Yes | Note capital S in TimeStamp |

---

## Response

| Status | Meaning |
|---|---|
| `200 OK` | Event processed and persisted successfully. Dapr acknowledges the message. |
| `5xx` | Processing failure (e.g., Cosmos DB unavailable). Dapr withholds acknowledgement; message is retried per the Azure Service Bus subscription policy. |
| `400 Bad Request` | Malformed payload that fails FluentValidation. Dapr acknowledges the message (poison-message prevention — no point retrying invalid data). |

---

## Controller Responsibility

The `EmailNotificationController` performs **only** these actions:

1. Receive the deserialized `EmailDeliveryReportReceivedEvent` from Dapr.
2. Construct and send a `SaveEmailDeliveryReportCommand` via `IMediator`.
3. Return `Ok()` on success.
4. Allow unhandled exceptions to propagate, resulting in a 5xx response (FR-009).

No business logic, logging (beyond endpoint entry), or data access is performed in the controller.

---

## Dapr Component Reference

| Environment | Component | Type |
|---|---|---|
| Local (Docker Compose) | `dapr/components/pubsub.yaml` | `pubsub.redis` — Redis on `redis:6379` |
| Azure (Container Apps) | Dapr component `pubsub` | `pubsub.azure.servicebus.queues` |

The topic `EmailDeliveryReportReceived` must exist as an Azure Service Bus topic (or queue, depending on component type) in the Service Bus namespace configured in the Dapr component.
