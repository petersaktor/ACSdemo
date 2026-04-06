<!--
SYNC IMPACT REPORT
==================
Version change: [placeholder] → 1.0.0
Constitution: ACSdemo Constitution — initial authoring from template.

Added principles:
  - I. CQRS Architecture with MediatR
  - II. Single-Project Structure
  - III. GraphQL-First API Design
  - IV. No Automated Testing (NON-NEGOTIABLE)
  - V. Dapr Pub/Sub Integration
  - VI. YAGNI & Simplicity

Added sections:
  - Technology Stack
  - Infrastructure & Security
  - Code Conventions
  - Governance

Removed sections: N/A (initial creation)

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check section is generic; compatible as-is.
  ✅ .specify/templates/spec-template.md — Test sections are already marked optional; compatible.
  ✅ .specify/templates/tasks-template.md — Test tasks already marked OPTIONAL; compatible with no-testing stance.

Deferred TODOs: None
-->

# ACSdemo Constitution

## Core Principles

### I. CQRS Architecture with MediatR

All business operations MUST be modelled as MediatR commands or queries.

- Commands MUST reside in `Application/Commands/`; queries in `Application/Queries/`.
- Every command/query file MUST co-locate three items: the request class, the handler class,
  and its FluentValidation validator.
- No service-to-service calls that bypass the MediatR pipeline are permitted.
- Direct invocation of infrastructure services from controllers (outside MediatR pipelines)
  is PROHIBITED.

**Rationale**: Centralises business logic, enforces consistent validation, and provides a
single auditable entry point for all state-changing operations.

### II. Single-Project Structure

ACSdemo MUST remain a single `.csproj` with the following logical folder hierarchy:

```
Domain/
Application/
Infrastructure/
Web/
```

Dependency rules (enforced by convention and code review):

- `Domain` MUST NOT reference `Infrastructure` or `Web`.
- `Application` MUST NOT reference `Web`.
- Every top-level folder MUST contain a `Configuration/` sub-folder exposing extension
  methods for DI registration.
- New assemblies MUST NOT be added without a written, team-approved justification recorded
  in the plan's Complexity Tracking table.

**Rationale**: Avoids distributed-project complexity while maintaining clean-architecture
boundaries within a single deployable unit.

### III. GraphQL-First API Design

GraphQL is the primary API surface, implemented with HotChocolate 15.x and Apollo Federation.

- All query resolvers MUST implement the `IGraphQLQuery` marker interface.
- All mutation resolvers MUST implement the `IGraphQLMutation` marker interface.
- All type extensions MUST implement the `IGraphQLType` marker interface.
- REST endpoints are permitted ONLY for:
  - Dapr pub/sub topic subscription handlers (decorated with `[Dapr.Topic()]`).
  - Health check endpoints (`/healthz`, `/readyz`).
- Adding new REST endpoints for business logic is PROHIBITED.

**Rationale**: Provides a consistent, schema-driven contract; Apollo Federation enables
future micro-frontend graph composition without service restructuring.

### IV. No Automated Testing (NON-NEGOTIABLE)

ACSdemo MUST NOT contain automated tests of any kind.

- No unit tests.
- No integration tests.
- No acceptance tests.
- Test projects MUST NOT be added to the solution.

Quality is assured through mandatory code review, static code analysis (Roslyn analyzers),
and manual verification against acceptance scenarios documented in each spec.

**Rationale**: Explicitly scoped out of this service's delivery model by team decision.

### V. Dapr Pub/Sub Integration

All inter-service and async communication MUST use Dapr pub/sub with Azure Service Bus as
the backing broker.

- Topic subscription endpoints MUST use the `[Dapr.Topic()]` attribute.
- All asynchronous processing (email dispatch, status updates, notifications) MUST be
  routed through pub/sub topics rather than in-process calls.
- Direct HTTP calls between services are PROHIBITED.

**Rationale**: Decouples services from transport details, enables message replay, and
leverages Azure Service Bus dead-lettering for durability.

### VI. YAGNI & Simplicity

The simplest working solution MUST be the default choice.

- Premature abstractions are PROHIBITED. Three similar code blocks are preferable
  to a premature shared helper.
- Decorator pattern (via Scrutor) is permitted ONLY when transparently wrapping an
  existing contract without altering its semantics.
- `Ardalis.GuardClauses` MUST be used for all precondition checks.
- No new infrastructure or design pattern may be introduced based on anticipated
  future requirements alone.

**Rationale**: Reduces cognitive overhead, speeds onboarding, and limits the surface
area for bugs by keeping the codebase as small as it needs to be.

## Technology Stack

The following package versions are LOCKED. Changes require a constitution amendment.

| Concern | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | net10.0 |
| CQRS | MediatR | 12.x |
| GraphQL | HotChocolate + Apollo Federation | 15.x |
| Validation | FluentValidation | 11.x |
| Mapping | AutoMapper | 15.x |
| Persistence | Azure Cosmos DB (`Microsoft.Azure.Cosmos`) | latest stable |
| Caching | Azure Managed Redis (`StackExchange.Redis`) | latest stable |
| Messaging | Azure Service Bus via Dapr pub/sub | Dapr 1.x |
| Email | Azure Communication Services Email SDK | latest stable |
| Identity | `Azure.Identity` — User-Assigned Managed Identity | latest stable |
| Configuration | Azure App Configuration + Azure Key Vault | latest stable |
| Containerization | Docker (Linux), Azure Container Apps | — |
| CI/CD | Azure DevOps Pipelines (YAML) | — |

Root namespace: `ACSdemo`.

## Infrastructure & Security

### Infrastructure as Code

- All Azure resources MUST be defined in Bicep templates under `.infrastructure/`.
- Manual Azure portal changes to production or staging resources are PROHIBITED.
- All environment-specific values (connection strings, resource names, SKUs) MUST be
  parameterized; hardcoded values are PROHIBITED.

### Secrets Management

- All secrets MUST reside in Azure Key Vault, referenced through Azure App Configuration.
- Secrets MUST NOT appear in source code, pipeline variables, or checked-in config files.
- `dotnet user-secrets` is permitted for local development only and MUST NOT be committed.

### Identity

- `Azure.Identity` with a User-Assigned Managed Identity MUST be the authentication
  mechanism for all Azure service access.
- Service principals or connection strings with embedded credentials are PROHIBITED in
  non-local environments.

## Code Conventions

- All constructor parameters MUST be validated with `Guard.Against.Null()` from
  `Ardalis.GuardClauses`.
- All service methods and MediatR handlers MUST be `async` and MUST accept and propagate a
  `CancellationToken`.
- Every handler and service class MUST inject `ILogger<T>` and use it for structured logging.
- Constructor injection is the ONLY permitted DI registration pattern.

## Governance

Amendments to this constitution MUST follow this procedure:

1. **Written Proposal**: The author submits a written proposal describing the change,
   motivation, and impact on existing features.
2. **Team Approval**: The proposal requires explicit team consensus before being applied.
3. **Semantic Versioning**:
   - MAJOR — Removal or incompatible redefinition of an existing principle.
   - MINOR — New principle or section added, or materially expanded guidance.
   - PATCH — Clarifications, wording fixes, or non-semantic refinements.
4. **Date Update**: `LAST_AMENDED_DATE` MUST be updated to the amendment date in ISO
   format `YYYY-MM-DD`.

All PRs MUST include a Constitution Check confirming no principles are violated. Violations
requiring an exception MUST be documented in the plan's Complexity Tracking table before
the PR is merged.

This constitution supersedes all prior verbal agreements, informal coding standards, and
conflicting wiki or README entries.

**Version**: 1.0.0 | **Ratified**: 2026-04-04 | **Last Amended**: 2026-04-05
