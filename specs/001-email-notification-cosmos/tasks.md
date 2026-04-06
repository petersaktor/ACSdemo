---
description: "Task list for 001-email-notification-cosmos"
---

# Tasks: ACSdemo Refactoring — Email Notification Controller

**Input**: Design documents from `/specs/001-email-notification-cosmos/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: No test tasks generated — Constitution Principle IV (No Automated Testing is NON-NEGOTIABLE).

**Organization**: Tasks grouped by user story. US1 is the independently-deliverable MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[US1/US2/US3]**: User story this task belongs to
- File paths are exact per plan.md Project Structure

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Upgrade project runtime, add all required packages, create folder scaffold, and rename infrastructure folder. All tasks are independent.

- [x] T001 [P] Upgrade `src/ACSdemo.Api/ACSdemo.Api.csproj` — set `<TargetFramework>net10.0</TargetFramework>`; add package references: `MediatR` 12.x, `HotChocolate.AspNetCore` 15.x, `HotChocolate.ApolloFederation` 15.x, `FluentValidation` 11.x, `Microsoft.Azure.Cosmos` latest stable, `Ardalis.GuardClauses` latest stable (do NOT remove old packages yet — legacy controllers still reference them)
- [x] T002 [P] Create logical folder hierarchy in `src/ACSdemo.Api/` — create empty `.gitkeep` files to establish: `Domain/Entities/`, `Domain/Configuration/`, `Application/Behaviours/`, `Application/Commands/`, `Application/Queries/`, `Application/Ports/`, `Application/Configuration/`, `Infrastructure/Persistence/`, `Infrastructure/Configuration/`, `Web/Controllers/`, `Web/GraphQL/Interfaces/`, `Web/GraphQL/`, `Web/Models/`, `Web/Configuration/`
- [x] T003 [P] Rename `deploy/` to `.infrastructure/`; create subdirectory `.infrastructure/modules/`; move `deploy/main.bicep` to `.infrastructure/main.bicep` and `deploy/containerapp.bicep` to `.infrastructure/modules/containerapp.bicep`; create empty placeholder files `.infrastructure/modules/cosmos.bicep`, `.infrastructure/modules/servicebus.bicep`, `.infrastructure/modules/appconfig.bicep`, `.infrastructure/modules/keyvault.bicep`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces, the Program.cs scaffold, and the Domain DI stub that ALL user stories depend on. Must complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 Create `Domain/Configuration/DomainServiceExtensions.cs` in `src/ACSdemo.Api/` — static class `DomainServiceExtensions` with extension method `AddDomainServices(this IServiceCollection services)` that returns `services` (no-op body); namespace `ACSdemo.Domain.Configuration`
- [x] T005 [P] Create `Application/Ports/IEmailDeliveryReportRepository.cs` in `src/ACSdemo.Api/` — interface `IEmailDeliveryReportRepository` with three methods: `Task SaveOrUpdateAsync(EmailDeliveryReport report, CancellationToken ct)`, `Task<EmailDeliveryReport?> GetByIdAsync(string messageId, CancellationToken ct)`, `Task<IReadOnlyList<EmailDeliveryReport>> GetByRecipientAsync(string recipientAddress, int skip, int take, CancellationToken ct)`; namespace `ACSdemo.Application.Ports`; add a `using ACSdemo.Domain.Entities;` forward reference (entity defined in T007)
- [x] T006 [P] Stub `src/ACSdemo.Api/Program.cs` — replace existing body with a clean ASP.NET Core 10 startup skeleton: `WebApplication.CreateBuilder`, placeholder comments `// TODO: US1 — builder.Services.AddApplicationServices()`, `// TODO: US1 — builder.Services.AddInfrastructureServices(builder.Configuration)`, `// TODO: US2 — builder.Services.AddWebServices()`, keep `builder.Services.AddControllers().AddDapr()`, keep `app.UseCloudEvents()`, `app.MapSubscribeHandler()`, `app.MapControllers()`; keep existing `ICommunicationService` registration temporarily; remove Swagger references

**Checkpoint**: Foundation complete — user story work can now proceed.

---

## Phase 3: User Story 1 — Automated Email Delivery Status Recording (Priority: P1) 🎯 MVP

**Goal**: When an `EmailDeliveryReportReceived` Dapr pub/sub event arrives, persist the delivery report to Cosmos DB with idempotent upsert and full status history.

**Independent Test**: Run `dapr publish --topic EmailDeliveryReportReceived` with a well-formed payload; confirm the document appears in the Cosmos DB `EmailDeliveryReports` container with correct fields and `statusHistory` array. Re-publish the same payload; confirm exactly one document exists (idempotency).

### Implementation for User Story 1

- [x] T007 [P] [US1] Create `src/ACSdemo.Api/Domain/Entities/EmailDeliveryReport.cs` — define `EmailDeliveryReport` class with properties: `string Id`, `string RecipientAddress`, `string SenderAddress`, `string CurrentStatus`, `DateTimeOffset LastEventTimestamp`, `List<DeliveryStatusEntry> StatusHistory`; constructor with `Guard.Against.Null()` / `Guard.Against.NullOrEmpty()` for Id, RecipientAddress, SenderAddress, StatusHistory; co-locate `DeliveryStatusEntry` record with properties `string Status`, `string? StatusMessage`, `DateTimeOffset Timestamp`; namespace `ACSdemo.Domain.Entities`
- [x] T008 [P] [US1] Create `src/ACSdemo.Api/Web/Models/EmailDeliveryReportReceivedEvent.cs` — define `EmailDeliveryReportReceivedEvent` record with properties matching ACS JSON schema: `string Sender`, `string Recipient`, `string MessageId`, `string Status`, `DeliveryStatusDetails? DeliveryStatusDetails`, `DateTimeOffset DeliveryAttemptTimeStamp` (capital S in TimeStamp); co-locate `DeliveryStatusDetails` record with `string? StatusMessage`; apply `[JsonPropertyName]` attributes to map PascalCase to camelCase JSON fields; namespace `ACSdemo.Web.Models`
- [x] T009 [P] [US1] Create `src/ACSdemo.Api/Application/Behaviours/ValidationBehavior.cs` — implement `IPipelineBehavior<TRequest, TResponse>` where `TRequest : IRequest<TResponse>`; constructor injects `IEnumerable<IValidator<TRequest>>` with `Guard.Against.Null()`; `Handle` method runs all validators via `ValidateAsync`; collects failures and throws `ValidationException` if any exist; namespace `ACSdemo.Application.Behaviours`
- [x] T010 [US1] Create `src/ACSdemo.Api/Application/Commands/SaveEmailDeliveryReport.cs` — co-locate three types in one file: (1) `SaveEmailDeliveryReportCommand` record implementing `IRequest<Unit>` with property `EmailDeliveryReportReceivedEvent Event`; (2) `SaveEmailDeliveryReportHandler` implementing `IRequestHandler<SaveEmailDeliveryReportCommand, Unit>` — constructor injects `IEmailDeliveryReportRepository`, `ILogger<SaveEmailDeliveryReportHandler>`; `Handle` reads existing document via `GetByIdAsync`, applies idempotency check (skip if `StatusHistory` already contains entry with same `DeliveryAttemptTimeStamp`), builds/updates `EmailDeliveryReport`, calls `SaveOrUpdateAsync`; (3) `SaveEmailDeliveryReportValidator` extending `AbstractValidator<SaveEmailDeliveryReportCommand>` — rules: `Event.MessageId` not empty, `Event.Recipient` not empty, `Event.Status` not null; namespace `ACSdemo.Application.Commands`; depends on T007, T008, T009, T005
- [x] T011 [P] [US1] Create `src/ACSdemo.Api/Infrastructure/Persistence/CosmosEmailDeliveryReportRepository.cs` — implement `IEmailDeliveryReportRepository`; constructor injects `CosmosClient`, `IConfiguration`, `ILogger<CosmosEmailDeliveryReportRepository>` with `Guard.Against.Null()`; reads container name and database name from `IConfiguration`; `SaveOrUpdateAsync` calls `container.UpsertItemAsync(report, new PartitionKey(report.RecipientAddress), cancellationToken: ct)`; `GetByIdAsync` runs cross-partition LINQ/SQL query `WHERE c.id = @messageId`; `GetByRecipientAsync` runs in-partition query `WHERE c.recipientAddress = @recipient ORDER BY c.lastEventTimestamp DESC OFFSET @skip LIMIT @take`; namespace `ACSdemo.Infrastructure.Persistence`; depends on T005, T007
- [x] T012 [US1] Create `src/ACSdemo.Api/Application/Configuration/ApplicationServiceExtensions.cs` — `AddApplicationServices(this IServiceCollection services)` calls: `services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()); cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)); })` and `services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`; namespace `ACSdemo.Application.Configuration`; depends on T009, T010
- [x] T013 [US1] Create `src/ACSdemo.Api/Infrastructure/Configuration/InfrastructureServiceExtensions.cs` — `AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)` registers `CosmosClient` as singleton using `new CosmosClient(configuration["CosmosDb:AccountEndpoint"], new DefaultAzureCredential())` (managed identity; falls back to `AccountKey` in development via user secrets); registers `IEmailDeliveryReportRepository` → `CosmosEmailDeliveryReportRepository` as scoped; namespace `ACSdemo.Infrastructure.Configuration`; depends on T011
- [x] T014 [US1] Create `src/ACSdemo.Api/Web/Controllers/EmailNotificationController.cs` — `[ApiController]`, `[Route("api/[controller]")]`; constructor injects `IMediator`, `ILogger<EmailNotificationController>` with `Guard.Against.Null()`; single action `HandleEmailDeliveryReport([FromBody] EmailDeliveryReportReceivedEvent evt, CancellationToken ct)` decorated `[HttpPost("email-delivery-report")]` and `[Topic("pubsub", "EmailDeliveryReportReceived")]`; sends `new SaveEmailDeliveryReportCommand(evt)` via mediator; logs entry with messageId; returns `Ok()`; unhandled exceptions propagate for non-2xx response (FR-009); namespace `ACSdemo.Web.Controllers`; depends on T008, T010
- [x] T015 [US1] Update `src/ACSdemo.Api/Program.cs` — (1) replace TODO placeholders with actual calls: `builder.Services.AddDomainServices()`, `builder.Services.AddApplicationServices()`, `builder.Services.AddInfrastructureServices(builder.Configuration)`; (2) wire Azure App Configuration as a configuration provider **before** other service registrations: `if (!string.IsNullOrEmpty(builder.Configuration["Azure:AppConfig:Endpoint"])) { builder.Configuration.AddAzureAppConfiguration(options => options.Connect(new Uri(builder.Configuration["Azure:AppConfig:Endpoint"]!), new DefaultAzureCredential())); }` — the null-guard means the block is skipped in local dev where the env var is absent; (3) keep the existing `ICommunicationService` singleton registration intact — legacy service files still exist and compile; full DI removal is deferred to T025 after T021–T024 delete the files; confirm `dotnet build` succeeds; depends on T004, T012, T013, T014

**Checkpoint**: User Story 1 fully functional — publish a Dapr event and verify Cosmos DB persistence and idempotency.

---

## Phase 4: User Story 2 — Email Delivery History Querying (Priority: P2)

**Goal**: Expose two GraphQL queries via HotChocolate 15 + Apollo Federation: retrieve a delivery report by message ID, and retrieve paginated reports by recipient address.

**Independent Test**: Open `http://localhost:5000/graphql` (Nitro IDE); execute `{ emailDeliveryReport(messageId: "...") { id currentStatus statusHistory { status timestamp } } }` and confirm correct data returned. Execute `emailDeliveryReportsByRecipient(recipientAddress: "...", skip: 0, take: 20)` and confirm paginated result with `hasNextPage`.

### Implementation for User Story 2

- [x] T016 [P] [US2] Create marker interfaces in `src/ACSdemo.Api/Web/GraphQL/Interfaces/` — three empty interfaces: `IGraphQLQuery`, `IGraphQLMutation`, `IGraphQLType`; namespace `ACSdemo.Web.GraphQL.Interfaces`
- [x] T017 [P] [US2] Create `src/ACSdemo.Api/Application/Queries/GetEmailDeliveryReports.cs` — co-locate **six** types (constitution Principle I — request + handler + validator per query): (1) `GetEmailDeliveryReportByIdQuery` record `IRequest<EmailDeliveryReport?>` with `string MessageId`; (2) `GetEmailDeliveryReportByIdHandler` implementing `IRequestHandler<GetEmailDeliveryReportByIdQuery, EmailDeliveryReport?>` — injects `IEmailDeliveryReportRepository`, `ILogger<GetEmailDeliveryReportByIdHandler>` with `Guard.Against.Null()`; logs `messageId` at Information on entry (FR-007); calls `GetByIdAsync(query.MessageId, ct)` passing `ct` (FR-008); (3) `GetEmailDeliveryReportByIdValidator` extending `AbstractValidator<GetEmailDeliveryReportByIdQuery>` — `RuleFor(q => q.MessageId).NotEmpty()`; (4) `GetEmailDeliveryReportsByRecipientQuery` record `IRequest<IReadOnlyList<EmailDeliveryReport>>` with `string RecipientAddress`, `int Skip = 0`, `int Take = 20`; (5) `GetEmailDeliveryReportsByRecipientHandler` — injects `IEmailDeliveryReportRepository`, `ILogger<GetEmailDeliveryReportsByRecipientHandler>` with `Guard.Against.Null()`; logs `recipientAddress` at Information on entry (FR-007); calls `GetByRecipientAsync(query.RecipientAddress, query.Skip, query.Take, ct)` passing `ct` (FR-008); (6) `GetEmailDeliveryReportsByRecipientValidator` extending `AbstractValidator<GetEmailDeliveryReportsByRecipientQuery>` — `RuleFor(q => q.RecipientAddress).NotEmpty()`, `RuleFor(q => q.Take).InclusiveBetween(1, 100)`; namespace `ACSdemo.Application.Queries`; depends on T005, T007
- [x] T018 [US2] Create `src/ACSdemo.Api/Web/GraphQL/EmailNotificationQueries.cs` — class `EmailNotificationQueries` decorated `[QueryType]`, implements `IGraphQLQuery`; two resolver methods: `GetEmailDeliveryReportAsync(string messageId, [Service] IMediator mediator, CancellationToken ct)` returning `Task<EmailDeliveryReport?>`; `GetEmailDeliveryReportsByRecipientAsync(string recipientAddress, int skip, int take, [Service] IMediator mediator, CancellationToken ct)` returning `Task<EmailDeliveryReportConnection>` where `EmailDeliveryReportConnection` is a co-located record with `IReadOnlyList<EmailDeliveryReport> Items`, `bool HasNextPage` (computed as `items.Count == take` — no separate count query per YAGNI/Principle VI; `TotalCount` removed); namespace `ACSdemo.Web.GraphQL`; depends on T016, T017, T007
- [x] T019 [US2] Create `src/ACSdemo.Api/Web/Configuration/WebServiceExtensions.cs` — `AddWebServices(this IServiceCollection services)` calls `services.AddGraphQLServer().AddQueryType<EmailNotificationQueries>().AddApolloFederation()`; namespace `ACSdemo.Web.Configuration`; depends on T018
- [x] T020 [US2] Update `src/ACSdemo.Api/Program.cs` — replace `// TODO: US2` placeholder with `builder.Services.AddWebServices()`; add `app.MapGraphQL()` after `app.MapControllers()`; confirm `dotnet build` succeeds and `/graphql` endpoint is reachable; depends on T019

**Checkpoint**: User Stories 1 and 2 both fully functional and independently testable.

---

## Phase 5: User Story 3 — Codebase Cleanup (Priority: P3)

**Goal**: Remove three legacy controllers, all exclusively-owned models and services, and stale package references. Build must be clean at end of phase.

**Independent Test**: Run `dotnet build` and confirm 0 errors, 0 warnings; confirm no source file in the repo contains the strings `EventsController`, `MessagesController`, `WeatherForecastController`, `ICommunicationService`, `SendEmailRequest`, `SendSmsRequest`.

### Implementation for User Story 3

- [x] T021 [P] [US3] Delete `src/ACSdemo.Api/Controllers/EventsController.cs`
- [x] T022 [P] [US3] Delete `src/ACSdemo.Api/Controllers/MessagesController.cs`
- [x] T023 [P] [US3] Delete `src/ACSdemo.Api/Controllers/WeatherForecastController.cs` and `src/ACSdemo.Api/WeatherForecast.cs`
- [x] T024 [P] [US3] Delete `src/ACSdemo.Api/Models/SendEmailRequest.cs`, `src/ACSdemo.Api/Models/SendSmsRequest.cs`, `src/ACSdemo.Api/Services/CommunicationService.cs`, `src/ACSdemo.Api/Services/ICommunicationService.cs`
- [x] T025 [US3] Finalize cleanup — (1) remove from `src/ACSdemo.Api/ACSdemo.Api.csproj`: `Azure.Communication.Email`, `Azure.Communication.Sms`, `Swashbuckle.AspNetCore` package references; (2) remove `ICommunicationService` singleton registration and all related `using` statements from `src/ACSdemo.Api/Program.cs` (safe to remove now because T021–T024 have deleted all files referencing it); run `dotnet build` and confirm 0 errors and 0 warnings; depends on T021, T022, T023, T024

**Checkpoint**: Legacy code fully removed — build clean, no dead references remain.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Complete Bicep infrastructure for Azure deployment; add configuration keys; verify end-to-end local scenario per quickstart.md.

- [x] T026 [P] Create `.infrastructure/modules/cosmos.bicep` — Cosmos DB account (serverless SKU), database `acsdemo`, container `EmailDeliveryReports` (partition key `/recipientAddress`, default TTL -1), composite index policy on `recipientAddress ASC` + `lastEventTimestamp DESC`; all resource names parameterized from `namePrefix`; no hardcoded keys — output endpoint URI for App Configuration
- [x] T027 [P] Create `.infrastructure/modules/servicebus.bicep` — Azure Service Bus namespace (Standard tier), topic `EmailDeliveryReportReceived`, subscription `acsdemo-api` with default lock duration and max delivery count; output connectionString secret reference for Key Vault
- [x] T028 [P] Create `.infrastructure/modules/appconfig.bicep` — Azure App Configuration resource; Key Vault references for `CosmosDb:AccountEndpoint`, `CosmosDb:DatabaseName` (value: `acsdemo`), `CosmosDb:ContainerName` (value: `EmailDeliveryReports`), `Dapr:PubSubName` (value: `pubsub`), `Dapr:TopicName` (value: `EmailDeliveryReportReceived`); all secrets reference Key Vault module outputs
- [x] T029 [P] Create `.infrastructure/modules/keyvault.bicep` — Azure Key Vault resource; secrets for Cosmos DB endpoint and Service Bus connection string; access policy granting the Container App User-Assigned Managed Identity `get` and `list` on secrets; output Key Vault URI
- [x] T030 [P] Update `.infrastructure/modules/containerapp.bicep` — add User-Assigned Managed Identity reference; update Dapr pub/sub component from `pubsub.azure.servicebus.queues` to `pubsub.azure.servicebus.topics`; add topic metadata `topic: EmailDeliveryReportReceived`; add env var `Azure__AppConfig__Endpoint` pointing to App Configuration endpoint (from appconfig module output); remove hardcoded empty secret values; reference Key Vault secrets via `secretRef`
- [x] T031 Update `.infrastructure/main.bicep` — wire all modules: cosmos, servicebus, keyvault, appconfig (passes keyvault URI), containerapp (passes appconfig endpoint and managed identity); all inter-module dependencies declared via module outputs; no hardcoded values
- [x] T032 [P] Update `src/ACSdemo.Api/appsettings.json` — add `"CosmosDb": { "AccountEndpoint": "", "DatabaseName": "acsdemo", "ContainerName": "EmailDeliveryReports" }` section; add `"Dapr": { "PubSubName": "pubsub", "TopicName": "EmailDeliveryReportReceived" }`; production values are empty — resolved from Azure App Configuration at runtime
- [x] T033 [P] Update `src/ACSdemo.Api/appsettings.Development.json` — set `"CosmosDb:AccountEndpoint"` to Cosmos DB Emulator URL (`https://localhost:8081`); set `"CosmosDb:DatabaseName"` to `acsdemo`; set `"CosmosDb:ContainerName"` to `EmailDeliveryReports`; add comment that `AccountKey` is managed via user secrets and MUST NOT be added to this file

---

## Dependencies

Story completion order and blocking relationships:

```
Phase 1 (T001, T002, T003) — all parallel, no inter-dependencies
    ↓
Phase 2 (T004, T005, T006) — T005 and T006 parallel; all depend implicitly on T002 (folders)
    ↓
Phase 3 / US1 (T007–T015) — MVP; T007+T008+T009+T011 parallel first batch
    → T010 depends on T007, T008, T009, T005
    → T012 depends on T010, T009
    → T013 depends on T011
    → T014 depends on T010, T008
    → T015 depends on T012, T013, T014
    ↓
Phase 4 / US2 (T016–T020) — T016+T017 parallel first batch; depends on Phase 3 ✅
    → T018 depends on T016, T017, T007
    → T019 depends on T018
    → T020 depends on T019
    ↓
Phase 5 / US3 (T021–T025) — T021+T022+T023+T024 parallel; T025 depends on all four
    ↓
Polish (T026–T033) — T026+T027+T028+T029+T030+T032+T033 parallel
    → T031 depends on T026–T030
```

---

## Parallel Execution Examples

### US1 — First parallel batch (after Phase 2 complete)
Run simultaneously:
- T007: `Domain/Entities/EmailDeliveryReport.cs`
- T008: `Web/Models/EmailDeliveryReportReceivedEvent.cs`
- T009: `Application/Behaviours/ValidationBehavior.cs`
- T011: `Infrastructure/Persistence/CosmosEmailDeliveryReportRepository.cs`

Then run T010 (depends on T007, T008, T009).  
Then run T012 and T013 in parallel.  
Then T014, then T015.

### US2 — First parallel batch (after T015 complete)
Run simultaneously:
- T016: `Web/GraphQL/Interfaces/*.cs` (3 marker interfaces)
- T017: `Application/Queries/GetEmailDeliveryReports.cs`

Then T018, T019, T020 sequentially.

### US3 — Full parallel batch (all deletions independent)
Run simultaneously: T021, T022, T023, T024. Then T025.

### Polish — Full parallel batch
Run simultaneously: T026, T027, T028, T029, T030, T032, T033. Then T031.

---

## Implementation Strategy

**MVP scope (suggested first delivery)**: Phases 1–3 only (T001–T015).  
After Phase 3, US1 is fully functional: Dapr event → MediatR → Cosmos DB persistence with idempotency.  
US2 (GraphQL queries, T016–T020) and US3 (cleanup, T021–T025) can follow as separate PRs.

**Total tasks**: 33  
**Tasks per story**: US1 = 9, US2 = 5, US3 = 5, Setup = 3, Foundational = 3, Polish = 8
