# Project Explanation

TransactionRiskEngine evaluates transaction risk with deterministic rules and stores the evidence behind each decision. It is designed around a practical fraud-review workflow: ingest a transaction, calculate risk signals, persist the decision trail, and expose the result to an operations UI.

## Core Question

For each transaction, the system answers:

```text
Should this transaction be approved, reviewed, or blocked?
```

The decision is based on multiple signals instead of a single fixed check.

## Data Model

The primary stored entities are:

- users;
- transactions;
- payment cards;
- devices;
- IP addresses;
- risk events;
- fraud cases;
- risk rules;
- audit logs;
- outbox messages;
- risk evaluation jobs.

Users can be linked to devices, cards, and IP addresses. Those links form a relationship graph that is used for indirect risk detection.

Transaction audit logs use an optional `AuditLogs.TransactionRecordId -> Transactions.Id` foreign key. Rule configuration, outbox messages, and evaluation jobs are standalone operational records rather than child records of one user.

## Transaction Analysis

The analyse endpoint performs a complete write workflow:

1. Validate transaction fields and idempotency input.
2. Confirm the user exists.
3. Resolve or create device, card, and IP records.
4. Link those records to the user.
5. Build risk signals.
6. Apply the current risk rule configuration.
7. Calculate the final score and decision.
8. Store the transaction, risk events, optional fraud case, audit log, and outbox messages.

The write is transactional. If one part fails, the database state is rolled back.

## Signal Types

### Sliding-Window Velocity

Velocity checks inspect recent activity windows, including:

- transaction count in the last 10 minutes;
- failed attempts in the last 15 minutes.

This catches bursts of activity that are risky even when a single transaction might look ordinary.

### Amount Anomaly

The amount detector compares the current amount with the user's previous successful transaction history.

Example:

```text
Historical average: NZD 80
Current amount:    NZD 1250
Signal:            HIGH_AMOUNT
```

The result is deterministic and stored as a risk event with reason and evidence.

### Relationship Graph

The graph logic checks indirect relationships between users and risky entities.

Example path:

```text
User Alex Morgan -> Device device-shared-risk-001 -> User Sam Risk
```

Traversal is bounded by depth, path count, visible node count, and expansion size so high-degree entities do not make a request unbounded.

### New Device

When a user appears with a device that has not previously been linked to their profile, the scoring flow can add a `NEW_DEVICE` signal.

## Risk Rules

Signal detection and scoring policy are separate.

Detection answers:

- did a suspicious pattern occur?
- what evidence supports it?
- what base severity did the detector assign?

Risk rules answer:

- is this signal currently enabled?
- how much score should it contribute?

Each stored risk event keeps both `BaseScore` and applied `Score`. This allows rule weights to change while preserving the original detector severity. High-amount and graph-risk events keep detector severity when it is stronger than the configured rule weight.

## Decision Thresholds

The final score is capped at 100.

```text
0-49     Approved
50-84    Review
85-100   Blocked
```

Review and blocked transactions can create fraud-case records for follow-up.

## Fraud Case Review

Fraud cases separate automated risk scoring from human review. The engine can open a case when a transaction reaches `Review` or `Blocked`. A reviewer can then move the case through a small lifecycle:

```text
Open -> Investigating -> ClosedApproved
Open -> Investigating -> ClosedBlocked
ClosedApproved / ClosedBlocked -> Investigating
```

Manual status changes can include a review note and are written to the audit log. Closing or reopening a case does not rewrite the original transaction decision; it records the review outcome around that decision. Rule evaluation can also reconcile case state when recalculated risk falls back to `Approved` or rises again.

## Rule Evaluation

When a risk rule changes, the system can evaluate recent transactions from stored risk events. Evaluating updates the applied scores and resulting decisions based on the current rule configuration.

This operation intentionally uses persisted risk-event facts. It does not reconstruct every historical graph or velocity window. High-amount events can upgrade stored detector severity when the saved amount maps to a stronger amount-risk band.

## Idempotency

Transaction ingestion accepts an idempotency key from either:

- `X-Idempotency-Key` header;
- `idempotencyKey` request body field.

When both are present, they must match.

The key is scoped by user. The stored transaction also keeps a SHA-256 hash of the request body. A retry with the same key and body returns the original response. A retry with the same key and different body returns a conflict.

## Outbox

The API writes outbox messages in the same transaction as the risk decision. A background worker then claims pending messages, retries failed dispatches, recovers stale processing locks, and delegates publishing to the configured publisher.

The current publisher can log messages locally or send them to an HTTP endpoint.

## Frontend

The Angular UI exposes the main operational workflows:

- submit a transaction and inspect the returned decision;
- browse and filter the review queue;
- load transaction detail and risk signals;
- inspect user relationship graphs;
- edit risk rules;
- start rule evaluation;
- view readiness, outbox state, and recent evaluation jobs.

The review queue uses RxJS streams for filters, pagination, refresh events, and detail loading.

## Testing

The backend tests cover:

- threshold decisions and score capping;
- amount anomaly boundaries;
- velocity and failed-attempt signals;
- graph traversal depth, cycle, and path limits;
- request validation;
- idempotency replay and conflict paths;
- rule application and rule bootstrapping;
- rule evaluation;
- outbox delivery and claim behaviour.

The frontend tests sit beside feature components and verify UI-level behaviour around API calls, state changes, and rendered output.
