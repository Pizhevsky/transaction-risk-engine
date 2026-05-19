# Architecture Decisions

## Minimal API Endpoint Groups

The backend uses ASP.NET Minimal APIs grouped by resource area:

- transactions;
- users;
- fraud cases;
- rules;
- health and status.

Endpoint files stay thin. Validation, scoring, graph loading, rule policy, outbox delivery, and rule evaluation live in services so the request handlers mostly coordinate HTTP concerns.

## Transactional Analysis Flow

The analyse workflow writes transaction records, risk events, fraud cases, audit logs, and outbox messages in one database transaction. This keeps the stored decision and the publishable event aligned.

The workflow uses explicit request validation before database mutation. Invalid transaction payloads return `422 Unprocessable Entity`.

## Idempotent Ingestion

Transaction ingestion accepts `X-Idempotency-Key` or a matching body `idempotencyKey`. The key is scoped by user and stored with a SHA-256 request hash.

Expected outcomes:

- same user, same key, same payload: original response with `200 OK` and `X-Idempotent-Replay: true`;
- same user, same key, different payload: `409 Conflict`;
- different users, same key: independent requests.

This protects client retries without hiding accidental key reuse.

## Signal Detection And Rule Scoring

Risk detectors produce base signals. Risk rules apply the current enabled and weight policy. Most signals use the configured rule weight; `HIGH_AMOUNT` and `GRAPH_RISK` keep the stronger detector severity when it exceeds the configured weight.

Stored risk events keep:

- signal code;
- detector base score;
- applied score;
- reason;
- evidence.

This allows rule evaluation to update current decisions while preserving the original detector output.

## Database Relationships

The ERD is centered on users and transactions. Transactions own risk events, optional fraud cases, and transaction audit-log links. Transactions also point to the resolved device, payment card, and IP address records, while separate user-entity link tables power relationship traversal.

`AuditLogs.TransactionRecordId` is optional because audit logs can describe non-transaction events, but transaction audit entries use a real foreign key so the relationship is visible in database tools. `RiskRules`, `OutboxMessages`, and `RiskEvaluationJobs` remain standalone operational tables.

## Rule Evaluating

Rule evaluation processes a bounded batch of recent transactions. It recalculates applied risk-event scores from persisted event codes and current rule configuration, then updates transaction decisions.

The operation is deterministic for the stored event set. It does not replay historical graph state or velocity windows. For high-amount events, it can upgrade stored detector severity when the saved amount maps to a stronger amount-risk band.

## Graph Traversal Guardrails

Graph risk is evaluated through bounded application-side BFS. The service limits:

- traversal depth;
- maximum returned paths;
- total visible nodes;
- edges expanded per node.

These limits keep request cost predictable for local and moderate datasets. A larger deployment could replace this with PostgreSQL recursive CTEs or a dedicated graph store while keeping the API contract stable.

## Outbox Dispatch

Domain events are written into the outbox inside the same transaction as the risk decision. A background worker claims pending messages, applies retry and backoff rules, requeues stale processing locks, and delegates publishing to `IOutboxPublisher`.

The publisher abstraction keeps local logging, HTTP publishing, and future broker publishing separated from the domain write flow.

## Health And Readiness

The API exposes:

- `/health/live` for process liveness;
- `/health/ready` for database connectivity and outbox backlog health;
- `/health/status` for diagnostic UI data.

Readiness returns a degraded status when the database is unavailable or the outbox backlog crosses configured safety conditions.

## Frontend Structure

Angular feature folders keep component logic, template, styles, and tests together. Routes are lazy-loaded so graph visualisation code is loaded only when the review area needs it.

The review queue uses RxJS streams to combine filters, pagination, refreshes, and detail loading. Request switching prevents stale detail responses from replacing the latest selected transaction.
