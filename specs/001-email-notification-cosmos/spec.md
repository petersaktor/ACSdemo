# Feature Specification: ACSdemo Refactoring — Email Notification Controller

**Feature Branch**: `001-email-notification-cosmos`  
**Created**: 2026-04-05  
**Status**: Draft  
**Input**: User description: "This application needs to be refactored. We no longer need the EventsController, MessagesController, and WeatherForecastController. They can be deleted. We will create a new one: It will be called the EmailNotification controller. This controller will receive the Microsoft.Communication.EmailDeliveryReportReceived event from Azure Service Bus and save it to Azure Cosmos DB. Users can then retrieve data from Azure CosmosDB using GraphQL queries."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Email Delivery Status Recording (Priority: P1)

When Azure Communication Services sends an `EmailDeliveryReportReceived` event via the
message bus, the system must automatically capture and durably persist the delivery report
so that the business has an authoritative audit trail of every email send attempt and its
outcome.

**Why this priority**: This is the core data-capture capability. Without it, no email
delivery history exists, making Story 2 impossible and the refactoring incomplete.

**Independent Test**: Publish a well-formed `Microsoft.Communication.EmailDeliveryReportReceived`
event to the designated topic and confirm the delivery report appears in the persistent
store with the correct status, timestamps, and message identifier.

**Acceptance Scenarios**:

1. **Given** a valid `EmailDeliveryReportReceived` event is published to the Azure Service
   Bus topic, **When** the system receives it, **Then** a delivery report record is created
   in the persistent store containing the message ID, recipient address, delivery status,
   and event timestamp.
2. **Given** a delivery report already exists for a message ID, **When** a subsequent
   `EmailDeliveryReportReceived` event arrives for the same message ID with a new status,
   **Then** the existing record is updated and the previous status is preserved in the
   history.
3. **Given** a malformed or incomplete event payload, **When** the system receives it,
   **Then** the event is rejected with a meaningful error log entry and no partial record
   is stored.

---

### User Story 2 - Email Delivery History Querying (Priority: P2)

Authorized consumers can query the stored email delivery reports in a flexible, structured
manner, enabling downstream systems or support teams to investigate delivery outcomes for
any given message or recipient.

**Why this priority**: Depends on Story 1 data capture being in place. Provides the
read-access layer that turns raw event data into actionable information.

**Independent Test**: With at least one persisted delivery report from Story 1, execute a
GraphQL query to retrieve it by message ID and confirm all fields are returned accurately.

**Acceptance Scenarios**:

1. **Given** one or more delivery reports are stored, **When** a consumer executes a
   GraphQL query for a specific message ID, **Then** the matching delivery report is
   returned with all recorded fields.
2. **Given** multiple delivery reports are stored, **When** a consumer queries by recipient
   address, **Then** all reports for that recipient are returned in reverse-chronological
   order.
3. **Given** no delivery reports match the query criteria, **When** the query executes,
   **Then** an empty result set is returned without an error.

---

### User Story 3 - Codebase Cleanup (Priority: P3)

The three legacy REST controllers (`EventsController`, `MessagesController`,
`WeatherForecastController`) and any models or services used exclusively by them are
removed from the codebase, eliminating dead code and reducing surface area.

**Why this priority**: Non-functional cleanup. Does not affect the delivery of Stories 1
or 2 but is part of the stated refactoring goal.

**Independent Test**: Build the project after removal and confirm zero compilation errors
and no remaining references to the deleted types.

**Acceptance Scenarios**:

1. **Given** the three legacy controllers exist in the codebase, **When** removal is
   applied, **Then** the project compiles successfully and no references to
   `EventsController`, `MessagesController`, or `WeatherForecastController` remain.
2. **Given** the legacy controllers are removed, **When** the application starts,
   **Then** it starts without errors and the remaining endpoints respond correctly.

---

### Edge Cases

- What happens when the message bus delivers the same event more than once (duplicate
  delivery)? The system must handle idempotent upserts based on message ID.
- What happens when the persistent store is temporarily unavailable? The event handler
  MUST return a non-2xx response so Dapr does not acknowledge the message; retry limits
  and dead-letter routing are governed entirely by the Dapr resiliency policy and the
  Azure Service Bus subscription configuration.
- What happens when the event contains an unrecognised delivery status value? The raw
  status string is stored as-is; no rejection occurs and no data is discarded.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST remove `EventsController`, `MessagesController`, and
  `WeatherForecastController` along with any exclusively-owned supporting files.
- **FR-002**: The system MUST expose a Dapr pub/sub subscription endpoint for the
  `Microsoft.Communication.EmailDeliveryReportReceived` event type.
- **FR-003**: On receipt of a valid `EmailDeliveryReportReceived` event, the system MUST
  persist the delivery report to the durable data store.
- **FR-004**: Delivery report persistence MUST be idempotent — re-processing the same
  event must not create duplicate records.
- **FR-005**: The system MUST expose a GraphQL query that allows retrieval of delivery
  reports by message ID.
- **FR-006**: The system MUST expose a GraphQL query that allows retrieval of delivery
  reports by recipient email address, ordered by most recent first, with offset/limit
  pagination (default page size: 20).
- **FR-007**: All structured log entries MUST include the message ID and event type for
  correlation.
- **FR-008**: The system MUST propagate a `CancellationToken` through all async operations
  involved in event processing and querying.
- **FR-009**: On any unhandled exception during event processing, the endpoint MUST return
  a non-2xx HTTP status so Dapr withholds acknowledgement and the message is retried;
  retry limits and dead-letter queue routing are owned by Dapr/Service Bus configuration.

### Key Entities

- **EmailDeliveryReport**: Represents the lifecycle of a single email send attempt as a
  single Cosmos DB document keyed by message ID. Key attributes: unique message identifier,
  recipient address (partition key), current delivery status, event timestamp, raw event
  payload, and a `statusHistory` array. Each element of `statusHistory` captures a status
  value and the timestamp at which that status was received, enabling full audit of all
  state transitions for a given message without cross-document lookups. The container is
  partitioned by `/recipientAddress` to co-locate all reports for a given recipient and
  optimise the by-recipient GraphQL query (FR-006).
- **DeliveryStatus**: The raw status string received from ACS, stored as-is without
  validation against a fixed list. The value originates from the
  `Microsoft.Communication.EmailDeliveryReportReceived` event payload and may include
  values such as Delivered, Failed, Bounced, or FilteredSpam, but the system treats the
  field as an open string to remain resilient to future ACS schema additions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Email delivery report events are captured and persisted within 5 seconds of
  the event being published to the message bus under normal operating conditions.
- **SC-002**: A GraphQL query for a known message ID returns the correct delivery report
  with all fields populated in under 1 second.
- **SC-003**: Duplicate event delivery (same message ID received twice) results in exactly
  one record in the persistent store with no data loss.
- **SC-004**: The project build produces zero errors and zero warnings related to the
  removed legacy controllers after cleanup.
- **SC-005**: The running application exposes no HTTP endpoints previously served by the
  three removed controllers.

## Clarifications

### Session 2026-04-05

- Q: How should delivery status history be stored in Cosmos DB when subsequent events arrive for the same message ID? → A: A single document per message ID; each new status is appended to an embedded `statusHistory[]` array.
- Q: What should be the partition key for the Cosmos DB container storing delivery reports? → A: `/recipientAddress` — all reports for a recipient co-located, optimising the by-recipient query (FR-006).
- Q: Should the GraphQL query for delivery reports by recipient address support pagination? → A: Offset/limit pagination with a default page size of 20.
- Q: Should DeliveryStatus be a closed enum or an open pass-through string? → A: Open string — the raw status value from ACS is stored as-is; no fixed-list validation.
- Q: Should the application enforce a maximum retry limit for failed Cosmos DB writes, or defer entirely to Dapr/Service Bus? → A: Defer entirely — application returns non-2xx on failure; retry limits and DLQ routing are owned by Dapr/Service Bus configuration.

## Assumptions

- Retry limits and dead-letter queue routing for failed event processing are owned by the
  Dapr resiliency policy and the Azure Service Bus subscription configuration; no
  application-level retry counter is implemented.
- The Dapr component configuration for the Azure Service Bus topic will be updated
  separately by the infrastructure team; this feature assumes the topic name and pubsub
  component name are available as configuration values.
- The `Microsoft.Communication.EmailDeliveryReportReceived` event schema from ACS is
  treated as the authoritative contract; no transformation of field names is required.
- Retention policy for delivery reports is not in scope for this feature; all records are
  stored indefinitely unless a future feature addresses archival.
- Authentication and authorisation for GraphQL query access follows the existing
  application-level security model and is not changed by this feature.
- The `WeatherForecast` model class (`WeatherForecast.cs`) is used only by
  `WeatherForecastController` and can be deleted alongside it.

