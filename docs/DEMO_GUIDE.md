# TransactionRiskEngine Demo Guide

## Purpose

This guide is a practical demo script for **TransactionRiskEngine .NET**. It walks through the backend API, Angular dashboard, rule configuration, graph explanation, operations screen, idempotent ingestion, validation, and outbox behaviour.

## Demo scope

The demo intentionally does **not** cover authentication or analyst roles. Those are out of scope for this project. The demo focuses on backend risk logic, API behaviour, rule scoring, idempotency, outbox operations, and frontend explainability.

## Assumptions

Seed data is enabled. The examples below assume these demo users are available:

| User | Id | Notes |
|---|---|---|
| Alex Morgan | `11111111-1111-1111-1111-111111111111` | Normal user with successful transaction history |
| Hana Patel | `22222222-2222-2222-2222-222222222222` | Additional normal demo user |
| Sam Risk | `33333333-3333-3333-3333-333333333333` | Flagged user connected to risky entities |

Known demo entities:

| Entity type | Value | Notes |
|---|---|---|
| Known Alex device | `device-alex-known-001` | Normal device for Alex |
| Risk linked device | `device-shared-risk-001` | Connected to flagged user Sam Risk |
| Known Alex card | `card-alex-known-001` | Normal card for Alex |
| Risk linked card | `card-shared-risk-001` | Connected to flagged user Sam Risk |
| Known Alex IP | `198.51.100.10` | Normal IP for Alex |
| Flagged IP | `203.0.113.99` | Directly flagged IP |

## Start the system

### 1. Start PostgreSQL 14

From the repository root:

```bash
docker compose up -d postgres
```

Expected result:

- PostgreSQL starts on `localhost:5432`
- database name is `transaction_risk`
- username is `postgres`
- password is `postgres`

### 2. Start the .NET API

```bash
cd backend/TransactionRiskEngine.Api
dotnet restore
dotnet run
```

Expected result:

- API starts at `http://localhost:5176`
- migrations are applied on startup
- default risk rules are bootstrapped
- demo seed data is available if `SeedData:Enabled` is true

### 3. Start the Angular frontend

Open a second terminal:

```bash
cd frontend
npm ci
npm start
```

Expected result:

- UI starts at `http://localhost:4200`
- Angular calls the API at `http://localhost:5176/api`

### 4. Check health endpoints

Open these URLs or call them with curl:

```http
GET http://localhost:5176/health/live
GET http://localhost:5176/health/ready
GET http://localhost:5176/health/status
```

Expected result:

- `/health/live` returns process status
- `/health/ready` returns database and outbox readiness
- `/health/status` returns diagnostic information for the Operations page

Notes:

- live means the process is alive
- ready means the service can safely receive traffic
- status is a diagnostic view for operations

## Frontend analysis behaviour

The Analyse screen is designed to give immediate feedback. After pressing **Analyse**, the current result appears directly below the form with:

- risk score and decision;
- transaction summary;
- generated risk signals;
- base score versus applied rule score when the current rule weight changes the signal;
- the stored score and decision that will also appear in the Review Queue.

This makes the simulation visible without needing to switch screens first. The Review Queue remains the place to inspect stored transactions, filters, pagination, and full detail/graph views.

## Demo flow overview

| Order | User case | Main screen or API | What it demonstrates |
|---:|---|---|---|
| 1 | Open Review Queue | Frontend | Transaction list, filters, pagination metadata |
| 2 | Open Operations | Frontend | Health, outbox, evaluation jobs |
| 3 | Analyse normal transaction | API or UI | Approved decision with low or no risk and an immediate result below the form |
| 4 | Analyse high amount | API or UI | Amount anomaly signal shown in the latest result panel |
| 5 | Analyse new device | API or UI | New device signal |
| 6 | Analyse failed attempts | API | Failed attempt signal |
| 7 | Analyse velocity spike | API | Sliding window signal |
| 8 | Analyse graph risk | API or UI | BFS relationship path and flagged entity evidence |
| 9 | Open transaction detail | Frontend | Explainable risk signals and base/applied score |
| 10 | Open relationship graph | Frontend | User, card, device, IP relationship view |
| 11 | Test idempotent replay | API | Same key and same body returns replay |
| 12 | Test idempotency conflict | API | Same key and different body returns 409 |
| 13 | Test request validation | API | 422 before database work |
| 14 | Change risk rule | Frontend or API | Configurable scoring policy |
| 15 | Evaluate transactions | Frontend or API | Existing risk events re-scored without changing evidence |
| 16 | Check outbox and status | Frontend | Operational visibility |

## User case 1: Open the Review Queue

### Steps

1. Open `http://localhost:4200`.
2. Go to the Review Queue screen.
3. Check the transaction table.
4. Change filters such as risk level, status, search text, page size, or page offset.

### Expected result

- Transactions are listed in descending creation order.
- The UI shows the visible result range and total count.
- Selecting a row loads detail safely.
- When filters change and the selected item disappears, the UI selects a valid visible item.

### Notes

The backend returns an array for frontend compatibility, but pagination metadata is exposed through response headers:

```text
X-Total-Count
X-Limit
X-Offset
```

The Angular UI consumes these headers rather than guessing pagination state from the body alone.

## User case 2: Open the Operations screen

### Steps

1. Open the Operations screen.
2. Check health, outbox status, rule count, and evaluation jobs.
3. Press Refresh.

### Expected result

- Health/status data is shown.
- The outbox backlog is visible.
- Last refreshed time updates.
- Loading state prevents duplicate refresh operations.

### Notes

This screen exists to show that backend operations are visible. It is not a decoration. It exposes health, outbox, and rule evaluation controls.

## User case 3: Analyse a normal transaction

### Request

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-normal-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 78,
    "currency": "NZD",
    "merchant": "Local Grocery",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "198.51.100.10",
    "successful": true
  }'
```

### Expected result

- First request returns `201 Created`.
- Decision is usually `Approved`.
- Risk score should be low because the amount, device, card, and IP match Alex's normal history.

### Notes

This proves the engine is not designed to flag everything. It can return a normal decision when the transaction matches the user's behaviour.

## User case 4: Analyse a high amount transaction

### Request

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-high-amount-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 1250,
    "currency": "NZD",
    "merchant": "Online Electronics Store",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "198.51.100.10",
    "successful": true
  }'
```

### Expected result

- Response includes `HIGH_AMOUNT`.
- Evidence should mention the current amount and recent average.
- Decision may be `Approved` or `Review` depending on current rule weights and other signals.

### Notes

The amount detector compares the transaction against recent successful history. It preserves a base score, then the rule engine applies the currently configured rule weight. Very large amounts can keep a stronger detector score than the default rule weight.

## User case 5: Analyse a new device transaction

### Request

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-new-device-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 95,
    "currency": "NZD",
    "merchant": "Book Store",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-new-alex-phone-001",
    "ipAddress": "198.51.100.10",
    "successful": true
  }'
```

### Expected result

- Response includes `NEW_DEVICE`.
- Evidence says the device had not been seen for Alex.

### Notes

Entity resolution links users to devices, cards, and IP addresses. The signal builder detects when a known user appears from a new device.

## User case 6: Analyse failed attempts

### Request

Send two failed transactions, then a successful transaction.

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-failed-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 80,
    "currency": "NZD",
    "merchant": "Payment Attempt",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "198.51.100.10",
    "successful": false
  }'

curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-failed-0002" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 82,
    "currency": "NZD",
    "merchant": "Payment Attempt",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "198.51.100.10",
    "successful": false
  }'

curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-failed-followup-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 84,
    "currency": "NZD",
    "merchant": "Payment Attempt Follow Up",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "1234",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "198.51.100.10",
    "successful": true
  }'
```

### Expected result

- Failed transactions can generate `FAILED_ATTEMPTS`.
- The final successful follow up should mention recent failed behaviour.

### Notes

The engine checks recent failed attempts in a sliding 15 minute window. This demonstrates temporal analysis, not only field validation.

## User case 7: Analyse velocity spike

### Steps

1. Send four quick transactions for the same user using different idempotency keys.
2. Send a fifth transaction.

### Request pattern

Use the normal transaction payload and change only:

```text
X-Idempotency-Key: demo-velocity-0001
X-Idempotency-Key: demo-velocity-0002
X-Idempotency-Key: demo-velocity-0003
X-Idempotency-Key: demo-velocity-0004
X-Idempotency-Key: demo-velocity-0005
```

### Expected result

- The later transaction should include `VELOCITY_SPIKE`.
- Evidence should mention the number of transactions in the last 10 minutes.

### Notes

The velocity detector uses a database count over a recent time window. This is algorithmic behaviour connected to indexed persistence.

## User case 8: Analyse graph risk

### Request

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -H "X-Idempotency-Key: demo-graph-risk-0001" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": 420,
    "currency": "NZD",
    "merchant": "Gift Card Marketplace",
    "cardFingerprint": "card-shared-risk-001",
    "cardLast4": "4242",
    "deviceFingerprint": "device-shared-risk-001",
    "ipAddress": "203.0.113.99",
    "successful": true
  }'
```

### Expected result

- Response includes `GRAPH_RISK`.
- Evidence should mention a flagged entity or a graph path.
- Decision is likely `Blocked` for the seeded risky device/card/IP values because directly flagged graph evidence keeps a strong detector severity.

### Notes

This is the key algorithm demo. The backend builds a bounded relationship graph across users, devices, cards, and IP addresses, then uses BFS to find explainable paths to risky entities.

## User case 9: Open transaction detail

### Steps

1. Open the Review Queue.
2. Select the graph-risk transaction.
3. Inspect the detail panel.

### Expected result

- Signals are visible.
- Each signal shows code, score, and evidence.
- If a rule weight changed, base score and applied score can differ.

### Notes

The UI is not hiding the algorithm. It exposes why a decision happened and separates original detector severity from applied rule score.

## User case 10: Open the relationship graph

### Steps

1. Select a risky transaction or open the graph panel for Alex.
2. Use the graph view.
3. Check nodes and edges for users, devices, cards, and IP addresses.

### API equivalent

```http
GET http://localhost:5176/api/users/11111111-1111-1111-1111-111111111111/connections
```

### Expected result

- The graph displays user relationships.
- Risky or flagged relationships are visible.
- Empty, loading, error, and retry states exist in the UI.

### Notes

The graph view is an investigation tool. It makes the BFS risk path understandable to a human reviewer.

## User case 11: Test idempotent replay

### Steps

1. Run the same `demo-normal-0001` request from User case 3 again with the same body.

### Expected result

- Response returns `200 OK`.
- Header `X-Idempotent-Replay: true` is present.
- No duplicate transaction is created.

### Notes

This protects clients from retrying a transaction after a timeout and accidentally creating duplicates.

## User case 12: Test idempotency conflict

### Steps

1. Reuse the same idempotency key from a previous successful request.
2. Change the body, for example change `amount` from `78` to `780`.

### Expected result

- Response returns `409 Conflict`.
- The engine refuses to treat a different request as a replay.

### Notes

The backend stores a request hash. Same user plus same key plus same payload is a replay. Same user plus same key plus different payload is a client error.

## User case 13: Test validation before database work

### Request

```bash
curl -i -X POST http://localhost:5176/api/transactions/analyse \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "11111111-1111-1111-1111-111111111111",
    "amount": -50,
    "currency": "NZDX",
    "merchant": "",
    "cardFingerprint": "card-alex-known-001",
    "cardLast4": "42AB",
    "deviceFingerprint": "device-alex-known-001",
    "ipAddress": "not-an-ip",
    "successful": true
  }'
```

### Expected result

- Response returns `422 Unprocessable Entity`.
- Validation errors are returned before risk scoring or database writes.

### Notes

Bad requests are rejected at the edge. This protects the risk engine and keeps data quality high.

## User case 14: Search, filter, and paginate transactions

### API examples

```http
GET http://localhost:5176/api/transactions?riskLevel=review&limit=10&offset=0
GET http://localhost:5176/api/transactions?status=failed&limit=10&offset=0
GET http://localhost:5176/api/transactions?search=Gift&limit=10&offset=0
```

### Expected result

- The body remains an array.
- Headers include total count, limit, and offset.
- Search uses PostgreSQL `ILIKE` and escapes wildcard input.

### Notes

The API remains simple for the frontend while still exposing enough metadata for correct pagination.

## User case 15: Update a risk rule

### Frontend steps

1. Open the Rules screen.
2. Change `HIGH_AMOUNT` weight, for example from `30` to `45`.
3. Save the rule.
4. Confirm the UI updates after success.

### API equivalent

```bash
curl -i -X PUT http://localhost:5176/api/rules/HIGH_AMOUNT \
  -H "Content-Type: application/json" \
  -d '{
    "description": "Transaction amount is materially above the user normal history.",
    "weight": 45,
    "enabled": true
  }'
```

### Expected result

- Rule update returns `200 OK`.
- Rule cache is invalidated.
- An audit log and outbox message are created.
- Future transactions use the new weight.

### Notes

Detection and scoring policy are separated. Detectors create raw evidence. Rules decide how much that evidence contributes.

## User case 16: Evaluate existing transactions

### Frontend steps

1. Open Operations.
2. Enter a batch size, for example `250`.
3. Enter a reason, for example `HIGH_AMOUNT weight changed`.
4. Start rule evaluation.

### API equivalent

```bash
curl -i -X POST http://localhost:5176/api/rules/evaluate \
  -H "Content-Type: application/json" \
  -d '{
    "batchSize": 250,
    "reason": "HIGH_AMOUNT weight changed"
  }'
```

### Expected result

- Response returns `202 Accepted`.
- An evaluation job is created.
- Existing risk events are re-scored using stored base scores and current rules.
- Stored high-amount events can be upgraded when the saved amount belongs to a stronger detector severity band.
- Original detector evidence is preserved.

### Notes

This is intentionally rule evaluation, not full historical reanalysis. It recalculates decisions from stored risk events after rule changes. It does not reconstruct every historical graph or velocity condition.

## User case 17: Check evaluation jobs

### API

```http
GET http://localhost:5176/api/rules/evaluation-jobs
```

### Expected result

- Recent jobs are listed.
- Processed count and changed count are visible.
- The Operations screen shows them.

### Notes

A rule change can have operational impact. The project exposes that impact instead of hiding it.

## User case 18: Check outbox behaviour

### Steps

1. Analyse a transaction.
2. Update a risk rule.
3. Evaluate recent transactions.
4. Open Operations.
5. Check outbox pending, failed, and readiness status.

### Expected result

- Transaction/rule events create outbox messages.
- Dispatcher claims messages before publishing.
- Retry and stale-lock design is represented in the domain.
- Local development can use logging or HTTP publisher configuration.

### Notes

Outbox messages are written transactionally with the domain decision, then published after commit through a publisher boundary. In a production deployment the publisher can be replaced with RabbitMQ, Kafka, SQS, or Azure Service Bus.

## User case 19: Demonstrate readiness degradation

### Steps

1. Open `/health/status`.
2. Open `/health/ready`.
3. If there is a critical outbox backlog or failed message count, readiness can return `503`.

### Expected result

- `/health/status` always gives diagnostic detail when the database is reachable.
- `/health/ready` is stricter because it tells orchestration whether the service should receive traffic.

### Notes

This separates diagnostic status from traffic readiness.

## User case 20: Demonstrate rate limiting

### Steps

1. Send many `POST /api/transactions/analyse` requests quickly.
2. Watch for rate-limit behaviour if the configured limit is exceeded.

### Expected result

- Write APIs are protected by a fixed-window rate limiter.
- Limits are configurable.

### Notes

This is not a full anti-abuse system, but it shows operational thinking around write endpoints.

## User case 21: Explain the frontend failure states

### Steps

1. Stop the API.
2. Refresh the frontend.
3. Open Review Queue, Operations, or Graph panel.

### Expected result

- UI shows loading or error states.
- Graph view has retry behaviour.
- Operations page shows unavailable status instead of silently failing.

### Notes

The frontend is built to expose backend state and failure clearly. This matters for internal analyst tools.

## Final demo checklist

Before recording or showing the project:

- PostgreSQL is running.
- Backend starts without migration errors.
- Frontend starts and reaches the API.
- `/health/ready` returns ready or a known diagnostic state.
- Review Queue shows transactions.
- Graph-risk transaction can be created.
- Transaction detail shows risk signals.
- Relationship graph loads.
- Rule update succeeds.
- Evaluation job can be created.
- Idempotency replay and conflict cases work.
- Validation returns `422` for bad input.

## Project notes

Key project properties:

- The final decision is deterministic and explainable.
- The graph algorithm is bounded, tested, and guarded against uncontrolled expansion.
- Idempotency includes request hash conflict detection, not only duplicate-key reuse.
- Rule changes affect applied scores while preserving original detector evidence.
- The outbox pattern keeps domain writes and publishable events consistent.
- The frontend makes algorithmic behaviour visible through detail panels, graph views, rules, and operations.
- Authentication and analyst roles are intentionally out of scope, but the first production step would be to protect rule update and evaluation endpoints with a `rules:write` claim.

## Suggested demo order for a 15 minute video

1. Show README and architecture in 60 seconds.
2. Start backend and frontend.
3. Open Operations and Review Queue.
4. Analyse a normal transaction.
5. Analyse a graph-risk transaction.
6. Show detail and graph evidence.
7. Show idempotent replay and conflict.
8. Change a risk rule.
9. Evaluate recent transactions.
10. Show operations/outbox status.
11. Finish with known trade-offs and next production steps.
