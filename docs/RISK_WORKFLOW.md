# Risk Workflow

This document describes how a transaction moves through the system from request validation to stored decision and operational review.

## 1. Request Intake

`POST /api/transactions/analyse` accepts transaction details:

- user ID;
- amount and currency;
- merchant;
- payment card fingerprint and last four digits;
- device fingerprint;
- IP address;
- success or failure status;
- optional idempotency key.

The API validates the request before any database mutation. Invalid input returns `422 Unprocessable Entity` with field-level errors.

## 2. Idempotency Check

The endpoint accepts an idempotency key through the `X-Idempotency-Key` header or request body. If both are supplied, they must match.

For a valid key, the scoring service checks whether the same user already submitted a transaction with that key:

- if the request hash matches, the original response is returned;
- if the request hash differs, the API returns `409 Conflict`;
- if no existing record is found, processing continues.

## 3. Entity Resolution

The service resolves the user's related entities:

- device by fingerprint;
- payment card by fingerprint;
- IP address by value.

Missing device, card, or IP records are created. The records are then linked to the user. This linking step provides the relationship data used by graph analysis.

## 4. Signal Building

Risk signals are built from several sources.

### Velocity

Recent transaction counts and failed-attempt counts are checked in time windows.

### Amount History

The current amount is compared with prior successful transaction amounts for the same user.

### Device State

The system records whether the device is new for the user.

### Graph Relationships

The graph service searches bounded paths from the user to flagged users or risky entities through shared devices, cards, and IP addresses.

## 5. Rule Application

Each generated signal has a code and base score. The risk rule catalog supplies the current policy for that code:

- enabled or disabled;
- applied weight;
- description.

Disabled rules do not contribute to the final score. Most enabled rules contribute their configured weight. `HIGH_AMOUNT` and `GRAPH_RISK` preserve detector severity when it is stronger than the configured weight; lower-severity failed-attempt signals can also keep their detector score.

## 6. Decision Calculation

Applied signal scores are summed and capped at 100.

```text
0-49     Approved
50-84    Review
85-100   Blocked
```

The response includes:

- transaction ID;
- final risk score;
- final decision;
- signal list;
- signal reasons and evidence.

## 7. Persistence

The API stores the following records in one transaction:

- transaction;
- risk events;
- fraud case when needed;
- audit log, with `TransactionRecordId` set for transaction audit entries;
- outbox messages.

The transaction boundary keeps the persisted decision and post-commit event stream consistent.

## 8. Outbox Dispatch

After commit, the background dispatcher:

1. requeues stale processing records;
2. claims a pending batch;
3. publishes each message;
4. marks messages as delivered or schedules another attempt.

Publishing can be local logging or HTTP delivery, depending on configuration.

## 9. Review UI

The Angular UI presents the stored state through:

- transaction submission;
- paged review queue;
- transaction detail;
- risk signal list;
- relationship graph;
- editable risk rules;
- rule evaluation action;
- readiness and outbox status.

The UI reads from the API rather than recalculating risk decisions in the browser.

## 10. Fraud Case Review

Fraud cases represent the review workflow around transactions that reached `Review` or `Blocked`.

A reviewer can:

- move an open case to `Investigating`;
- return an investigating case to `Open`;
- close a case as approved;
- close a case as blocked;
- reopen a closed case for more investigation.

The review action can carry an optional note. Status changes are audited and closing a case records `ClosedAt`. This workflow does not change the original transaction decision. It records the manual review outcome and keeps it separate from deterministic scoring.

The frontend protects this workflow with per-case pending state. While a case update is saving, all actions for that case are disabled and only the selected action shows `Saving...`; other cases remain interactive.

