# TBM API Security Threat Model

Generated on: February 22, 2026

## 1. Scope

This document covers externally reachable API surfaces:

- `/api/v1/auth/*`
- `/api/v1/checkout/*`
- `/api/v1/ai/*`
- `/api/webhooks/paystack`
- `/api/admin/*`

## 2. Critical Threats and Controls

### 2.1 Credential Stuffing and Brute Force

Threat:
- Repeated login/reset attempts against user and admin auth endpoints.

Controls:
- Strict auth-sensitive rate limit profile (IP partitioned).
- 429 response includes `Retry-After`.
- Request ID attached to every rejected request for incident tracing.

Residual risk:
- Account lockout/MFA challenge escalation is not yet implemented.

### 2.2 Payment Replay and Duplicate Checkout

Threat:
- Duplicate payment requests creating duplicated orders.

Controls:
- Idempotency key required for checkout payment flow.
- Duplicate key replay returns idempotent response, not duplicate order.
- Payment amount mismatch on reused key is blocked.

Residual risk:
- Provider-specific idempotency reconciliation dashboards can be expanded.

### 2.3 Webhook Replay/Abuse

Threat:
- Forged or replayed webhook payloads changing order/payment state.

Controls:
- Signature validation on `x-paystack-signature`.
- Webhook event idempotency based on provider reference.
- Dedicated webhook rate-limiter policy.

Residual risk:
- Additional anti-replay nonce windowing can be added.

### 2.4 API Flood / DoS

Threat:
- High-volume request floods causing service degradation.

Controls:
- Dynamic policy with tuned buckets for auth, checkout-payment, AI generation, admin, and default traffic.
- Policy partitioning by user identity or client IP.
- Centralized `OnRejected` telemetry + response envelope.

Residual risk:
- Network edge protection (WAF/CDN rules) should be enforced at infrastructure layer.

### 2.5 Observability and Forensics Gaps

Threat:
- Insufficient telemetry during incidents.

Controls:
- Request/correlation IDs injected into responses.
- Structured request logs with method/path/status/latency/user/client.
- In-memory SLO metrics for `auth`, `checkout`, and `ai`, exposed via admin endpoint.

Residual risk:
- External log aggregation and long-term retention are still required.

## 3. Review Cadence

- Re-review this threat model after each major phase or architecture change.
- Mandatory review triggers:
  - new payment provider
  - new webhook source
  - new public unauthenticated endpoint
  - AI provider changes
