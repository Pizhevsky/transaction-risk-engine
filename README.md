# TransactionRiskEngine

TransactionRiskEngine is a full-stack transaction risk analysis system. It accepts card transaction events, evaluates deterministic fraud-risk signals, stores the decision trail, and presents the results in an Angular operations UI.

The backend is a .NET API backed by PostgreSQL. The frontend is an Angular/RxJS dashboard for submitting transactions, reviewing scored transactions, inspecting relationship graphs, editing risk rules, and monitoring operational status.

## Capabilities

- Transaction analysis with approve, review, and block decisions.
- Sliding-window velocity checks for recent transaction activity and failed attempts.
- Amount anomaly detection against each user's successful transaction history.
- Relationship analysis across users, devices, payment cards, and IP addresses.
- Explainable risk signals with code, score, reason, and evidence fields.
- Configurable risk rules stored in PostgreSQL.
- Rule evaluation for existing risk events without rewriting the original detector output.
- Idempotent transaction ingestion with request hash conflict detection.
- Transactional writes for transactions, risk events, fraud cases, audit logs, and outbox messages.
- Claimed outbox dispatch with retry, backoff, and stale-lock recovery.
- Health, readiness, and diagnostic endpoints.
- Angular review queue, graph view, rules screen, and operations screen.

## Tech Stack

### Backend

- .NET 10 Web API with Minimal APIs
- PostgreSQL 14
- Entity Framework Core with Npgsql
- OpenAPI in development
- Correlation ID middleware
- Fixed-window rate limiting for write endpoints
- Background outbox dispatcher
- xUnit tests
- Optional PostgreSQL integration tests through Testcontainers

### Frontend

- Angular standalone components
- TypeScript
- RxJS
- Cytoscape.js for relationship graph visualisation
- Component-level unit tests

### Infrastructure

- Docker Compose file for PostgreSQL 14
- Local API and UI runtime without containerising the application services

## Repository Layout

```text
TransactionRiskEngine/
  backend/
    TransactionRiskEngine.Api/
      Data/                         # EF Core DbContext and seed data
      Domain/                       # Persisted domain models
      Dtos/                         # API request and response contracts
      Endpoints/                    # Minimal API endpoint groups
      Infrastructure/               # Correlation ID middleware and runtime accessors
      Services/
        Graph/                      # Graph models, traversal, and graph-risk lookup
        Outbox/                     # Outbox writing, claiming, publishing, and dispatch
        Risk/                       # Scoring, decisions, and manual evaluation
          Rules/                    # Rule snapshots, options, and score application
          Signals/                  # Risk signal detection and evidence building
        Transactions/               # Idempotency, mapping, and entity resolution
      Startup/                      # Database and rule bootstrapping
      Validation/                   # Request validation
    TransactionRiskEngine.Tests/
      Integration/                 # PostgreSQL/Testcontainers API flow tests
      Services/
        Graph/                     # Graph traversal and risk graph service tests
        Outbox/                    # Outbox claim, delivery, and publisher tests
        Risk/                      # Risk score, decision, and evaluation tests
          Rules/                   # Rule catalog and rule application tests
          Signals/                 # Signal detection and evidence tests
        Transactions/              # Transaction mapping and idempotency tests
      Support/                     # Shared test factory and test attributes
      Validation/                  # Request validation and security tests
  frontend/
    src/app/core/                   # API client and shared models
    src/app/features/               # Analysis, review, rules, and operations screens
    src/app/layout/                 # Route-driven shell menu
    src/app/shared/                 # Shared standalone UI components
  docs/
    ARCHITECTURE_DECISIONS.md
    DEMO_GUIDE.md
    EXPLANATION.md
    RISK_WORKFLOW.md
  docker-compose.yml
```

## Prerequisites

- .NET SDK 10
- Node.js and npm
- PostgreSQL 14, either local or through Docker

Optional checks:

```bash
dotnet --version
node --version
npm --version
psql --version
```

## Start PostgreSQL

From the repository root:

```bash
docker compose up -d postgres
```

Default connection string:

```text
Host=localhost;Port=5432;Database=transaction_risk;Username=postgres;Password=postgres
```

To stop the database:

```bash
docker compose down
```

## Run The Backend

```bash
cd backend/TransactionRiskEngine.Api
dotnet restore
dotnet run
```

Default API URL:

```text
http://localhost:5176
```

OpenAPI JSON is available in development:

```text
http://localhost:5176/openapi/v1.json
```

On startup the API applies pending EF Core migrations when `Database:UseMigrations` is enabled, then ensures default risk rules exist. Sample seed data is controlled separately by `SeedData:Enabled`.

## Run The Frontend

In a separate terminal:

```bash
cd frontend
npm ci
npm start
```

Default UI URL:

```text
http://localhost:4200
```

The Angular app uses this API base URL by default:

```text
http://localhost:5176/api
```

Frontend environment files:

```text
frontend/src/environments/environment.ts
frontend/src/environments/environment.development.ts
```

## Configuration

Backend configuration is in:

```text
backend/TransactionRiskEngine.Api/appsettings.json
backend/TransactionRiskEngine.Api/appsettings.Development.json
```

Key sections:

- `ConnectionStrings:RiskDb`: PostgreSQL connection string.
- `Cors:AllowedOrigins`: allowed frontend origins.
- `RateLimiting:WritePermitLimit`: per-minute write limit.
- `Outbox`: dispatcher polling, batch size, attempt limit, and stale-lock timing.
- `Outbox:Publisher`: logging or HTTP publishing configuration.
- `RiskRules`: rule cache configuration.
- `GraphRisk`: graph depth, path, node, and expansion limits.

## Database Relationships

The core ERD relationships are:

- `Users` -> `Transactions`
- `Transactions` -> `RiskEvents`
- `Transactions` -> `FraudCases`
- `Transactions` -> `AuditLogs`
- `Transactions` -> `Devices`, `PaymentCards`, and `IpAddresses`
- `Users` <-> `Devices`, `PaymentCards`, and `IpAddresses` through link tables

`AuditLogs.TransactionRecordId` is an optional foreign key for transaction audit entries, so pgAdmin can draw the relationship to `Transactions.Id`. `RiskRules`, `OutboxMessages`, and `RiskEvaluationJobs` are standalone configuration or operations tables by design; they are not owned by a single user or transaction.

## Demo Guide

A step-by-step demo script is available in [`docs/DEMO_GUIDE.md`](docs/DEMO_GUIDE.md). It covers the main product paths: analysing transactions, reviewing decisions, inspecting graph relationships, changing rule weights, running evaluation jobs, checking idempotency behaviour, and using the operations screen.

For the quickest frontend demo, open the Analyse screen, keep the seeded risky values, press **Analyse**, and review the latest result directly below the form. The same transaction then appears in the Review Queue with its stored decision trail.

## API Examples

### Analyse A Transaction

```http
POST http://localhost:5176/api/transactions/analyse
Content-Type: application/json
X-Idempotency-Key: transaction-0001

{
  "userId": "11111111-1111-1111-1111-111111111111",
  "amount": 1250,
  "currency": "NZD",
  "merchant": "Online Electronics Store",
  "cardFingerprint": "card-shared-risk-001",
  "cardLast4": "4242",
  "deviceFingerprint": "device-shared-risk-001",
  "ipAddress": "203.0.113.99",
  "successful": true
}
```

Invalid requests return `422 Unprocessable Entity` with field-level validation details. A repeated idempotency key for the same user and identical request body returns the original response with `200 OK` and `X-Idempotent-Replay: true`. Reusing the same key for the same user with a different body returns `409 Conflict`.

### List Transactions

```http
GET http://localhost:5176/api/transactions?riskLevel=review&limit=50&offset=0
```

Pagination metadata is returned in headers:

```text
X-Total-Count
X-Limit
X-Offset
```

### Get Transaction Detail

```http
GET http://localhost:5176/api/transactions/{transactionId}
```

The detail response includes transaction metadata, linked device/card/IP fields, final decision, and risk signals.

### Get User Connections

```http
GET http://localhost:5176/api/users/{userId}/connections
```

The graph response contains visible nodes, edges, and risk paths used by the frontend graph panel.

### Update A Risk Rule

```http
PUT http://localhost:5176/api/rules/HIGH_AMOUNT
Content-Type: application/json

{
  "description": "Transaction amount is materially above the user's normal history.",
  "weight": 35,
  "enabled": true
}
```

### Evaluate Recent Transactions

```http
POST http://localhost:5176/api/rules/evaluate
Content-Type: application/json

{
  "batchSize": 250,
  "reason": "HIGH_AMOUNT rule weight changed"
}
```

### Health And Status

```http
GET http://localhost:5176/health/live
GET http://localhost:5176/health/ready
GET http://localhost:5176/health/status
```

`/health/live` confirms the process is running. `/health/ready` checks database connectivity and outbox backlog health. `/health/status` returns diagnostic data used by the operations screen.

## Risk Workflow

1. The API validates the transaction request and idempotency key.
2. Existing user, device, card, and IP records are resolved or linked.
3. Risk signal builders inspect recent velocity, failed attempts, amount history, new device state, and graph connections.
4. Current risk rules decide which signals contribute to the applied score. High-amount and graph-risk signals keep detector severity when it is stronger than the configured rule weight.
5. The decision service normalises the total score into `Approved`, `Review`, or `Blocked`.
6. The API writes the transaction, risk events, optional fraud case, audit log, and outbox messages in one database transaction.
7. The outbox dispatcher claims and publishes committed messages after the transaction completes.

Default decision thresholds:

```text
0-49     Approved
50-84    Review
85-100   Blocked
```

## Frontend Behaviour

- The Analyse screen shows the latest decision directly below the form after submission, including score, decision, and generated signals.
- The review queue combines search, risk filter, status filter, pagination, refresh events, and safe detail loading.
- Detail loading uses request switching so stale responses do not replace a newer selection.
- The graph panel has loading, empty, error, and retry states.
- The rules screen supports rule editing and evaluation submission.
- The operations screen shows readiness, outbox status, recent evaluation jobs, and last refresh time.
- Routes are lazy-loaded so graph dependencies are loaded only when needed.

## Tests

Backend unit tests:

```bash
cd backend
dotnet test
```

Optional PostgreSQL integration tests:

```bash
cd backend
RUN_POSTGRES_INTEGRATION_TESTS=true dotnet test
```

Frontend checks:

```bash
cd frontend
./node_modules/.bin/tsc -p tsconfig.app.json --noEmit
./node_modules/.bin/tsc -p tsconfig.spec.json --noEmit
npm test
```

The test suite covers risk thresholds, amount anomaly detection, velocity signals, graph traversal, request validation, rule application, idempotency, rule evaluation, outbox delivery, outbox claiming, and selected API paths.

## Trade-Offs

- Authentication and role-based access control are not included.
- Offset pagination is used for transaction lists.
- Relationship traversal is bounded application-side BFS, not a database recursive query.
- Outbox publishing can log locally or send HTTP requests; a message broker adapter is not included.
- Evaluating recalculates decisions from stored risk events and current rules. It does not replay historical graph or velocity context; high-amount events can upgrade stored detector severity when the saved amount maps to a stronger amount-risk band.
