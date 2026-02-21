# TBM Full Implementation Roadmap

Generated on: February 21, 2026  
Inputs:
- `docs/TBM-API-Documentation.md`
- `docs/Frontend-Backend-Alignment-Report.md`
- `docs/frontend-backend-alignment-matrix.csv`
- `docs/frontend doc.md`

## 1. Objective

Deliver full backend implementation aligned to frontend contract, while preserving production quality across security, observability, and maintainability.

Primary outcomes:
- Close `Live` and `Local` integration gaps immediately.
- Convert `Mock` and `Inferred` frontend domains into real backend APIs in phased releases.
- Harden AI for production (media persistence, usage/cost tracking, quota/credits).

## 2. Current Baseline

- Backend implemented controller action methods: `116`
- Frontend contract rows: `111`
- Strict contract coverage: `65` capability-backed, `46` missing
- Live + Local integration coverage: `11/11` capability-backed, `0` missing

Current P0 gaps:
- OAuth currently runs in disabled mode (`501` actionable response) for: `GET /auth/google`, `GET /auth/apple`
- Path normalization mismatch: `/Auth/*` vs `/api/v1/Auth/*`
- Verify-email response contract still needs frontend/client normalization across both variants

## 3. Delivery Principles

- Keep one canonical API namespace: `/api/v1/*` and `/api/admin/*`.
- Add temporary compatibility aliases for legacy frontend paths; remove after frontend migration.
- Standardize response envelope for new endpoints (`ApiResponse<T>` + consistent error model).
- Every phase must include tests, Swagger/OpenAPI updates, and docs updates.

## 4. Phased Roadmap

## Phase 0: Contract Stabilization (Week 1)

Goal: Make current frontend `Live + Local` flows reliable with minimal friction.

Scope:
- Add compatibility routing for `/Auth/*` paths (or frontend immediate switch to `/api/v1/auth/*`).
- Implement OAuth endpoints or explicitly disable in frontend and return actionable error contract.
- Add verify-email body flow (`POST /api/v1/auth/verify-email` with `{ email, code }`) while retaining token query flow.
- Confirm `/api/auth/me` proxy compatibility contract.
- Decide and document canonical route casing strategy.

Acceptance criteria:
- `Live + Local` coverage moves from `8/11` to `11/11` capability-backed.
- No frontend auth flow blocked by route mismatch.
- Contract test suite passes for all auth endpoints.

Current status:
- Route compatibility aliases implemented for auth paths.
- Verify-email supports both token query and `{ email, code }` body.
- OAuth routes exist in disabled mode with actionable responses.
- `/api/flooring` compatibility endpoint is implemented.

## Phase 1: Commerce Core Completion (Weeks 2-4)

Goal: Complete shopping and checkout paths.

Scope:
- Catalog compatibility layer:
  - `/api/flooring`
  - `/materials`
  - `/materials/:id`
- Cart enhancements:
  - `/api/cart/apply-promo`
  - `/api/cart/related`
- Checkout APIs:
  - `/api/checkout`
  - `/api/checkout/payment`
  - `/api/checkout/validate-promo`
- Orders extension:
  - `/api/orders/:orderId/invoice`

Data/model work:
- Promo/discount model
- Checkout summary model
- Invoice generation/storage strategy

Acceptance criteria:
- End-to-end cart -> checkout -> order -> invoice works in staging.
- Payment workflow is idempotent.
- Promo validation includes abuse prevention and audit trail.

Current status:
- Compatibility catalog/cart/checkout/order-invoice routes implemented.
- Promo validation + abuse throttling + audit trail implemented.
- Checkout payment endpoint supports idempotency key handling.

## Phase 2: User Data, Saved Items, and Design Library (Weeks 5-7)

Goal: Complete user-facing account and personalization domains.

Scope:
- Saved/moodboard domain:
  - `/api/saved` CRUD + board actions + buy-all
- Designs library:
  - `/api/designs` list/detail
  - favorite/delete/download/share flows
- Dashboard widget APIs:
  - recent order, latest design, consultations, saved preview, tracking link
- Account APIs:
  - profile, email, phone, addresses CRUD/default
  - password update
  - security/2FA preference
  - notifications preference
  - avatar upload
  - deactivate/delete account

Acceptance criteria:
- All account pages and user dashboard pages run without mock data.
- Address and security flows are fully authorized and audited.
- Saved/design actions are ownership-protected by user ID.

Current status:
- `/api/saved`, `/api/designs`, `/api/dashboard/*`, and `/api/account/*` route set implemented.
- Address and security preference updates are authorized and audited.
- Saved/design operations enforce user ownership checks.

## Phase 3: AI Production Hardening (Weeks 8-9)

Goal: Move AI from functional to production-grade.

Scope:
- Generated media persistence pipeline:
  - Poll success from provider
  - Download media
  - Upload to Cloudinary
  - Persist Cloudinary URL in `AIDesign.OutputUrl`
  - Store provider raw metadata and temporary URL separately
- `AIUsage` implementation:
  - Persist per-generation usage (provider, generation type, estimated cost, credits used)
  - Aggregate user usage and monthly spend
- Credits/quotas:
  - Credit balance model
  - Deduct credits per generation
  - Block generation when balance is insufficient
  - Admin credit adjustment endpoints

Acceptance criteria:
- No temporary provider URL is used as final user asset URL.
- Every AI generation writes an `AIUsage` record.
- Credit guard is enforced before provider call.
- Admin can top-up/reverse credits with audit log.

## Phase 4: Vendor Portal Domain (Weeks 10-12)

Goal: Implement vendor operations as a first-class backend domain.

Scope:
- Introduce `Vendor` role and access model.
- Vendor dashboard/stats/alerts/activity.
- Vendor orders and fulfillment:
  - status updates, notes, assignment, search/filter.
- Inventory management APIs.
- Delivery assignments APIs.
- Vendor messages and notifications APIs.

Foundational decisions required:
- Vendor tenancy/data partition model.
- Vendor-to-product ownership model.
- Vendor SLA and event-driven notifications.

Acceptance criteria:
- Vendor users can operate without admin privileges.
- Vendor APIs are role-isolated and ownership-validated.
- Pagination/filter contracts match frontend needs.

## Phase 5: Admin Expansion and Operations (Weeks 13-14)

Goal: Close remaining admin mock/inferred contracts.

Scope:
- Admin dashboard endpoints (stats, revenue, server load, alerts, quick actions, refresh/export).
- Admin settings fine-grained endpoints:
  - notifications settings
  - payment gateway toggle patch
  - AI model toggle patch
- Admin system logs endpoints + export.
- Admin financial endpoints:
  - stats
  - monthly revenue
  - revenue by service
  - transactions + export

Acceptance criteria:
- Admin portal removes dependency on frontend mock data.
- Financial and log exports are permissioned and auditable.

## Phase 6: Platform Hardening (Parallel, Every Phase)

Scope:
- OpenAPI-first contract generation and validation in CI.
- Contract tests for route, schema, and auth.
- Observability:
  - request IDs
  - structured logs
  - API latency and error dashboards
- Security:
  - threat model updates
  - rate-limit tuning
  - idempotency keys for payment and webhook-sensitive flows
- Performance:
  - query tuning and caching strategy
  - pagination and index reviews

Acceptance criteria:
- CI blocks contract-breaking changes.
- SLO dashboards exist for auth, checkout, and AI endpoints.

## 5. Implementation Backlog by Priority

## P0 (Immediate)
- Auth route compatibility + casing strategy.
- OAuth endpoints decision/implementation.
- Verify-email dual contract support.
- Freeze canonical auth response contract for frontend.

## P1 (Near-term)
- Commerce checkout completion.
- User saved/design/account domains.
- Admin domain expansion.

## P2 (Mid-term)
- Vendor portal full domain.
- Advanced reporting/exports and operational polish.

## 6. Definition of Done (Per Feature)

A feature is done only when all are true:
- Endpoint implemented with auth/role checks.
- Request/response schema documented in Swagger and docs.
- Automated tests added (unit + integration contract).
- Failure/error cases standardized.
- Observability and audit requirements met.
- Frontend integration validated in staging.

## 7. Suggested Delivery Cadence

- Weekly contract sync (Backend + Frontend leads)
- Weekly release train to staging
- Bi-weekly production releases
- Roadmap checkpoint every 2 weeks against `frontend-backend-alignment-matrix.csv`

## 8. Tracking KPIs

- Live+Local capability coverage target: `11/11`
- Strict contract capability coverage target:
  - Milestone A: `40+`
  - Milestone B: `70+`
  - Milestone C: `100+`
- AI production readiness:
  - 100% generated assets persisted to Cloudinary
  - 100% AI generations tracked in `AIUsage`
  - 0 unguarded generation attempts when credits exhausted

## 9. Immediate Next Steps (This Week)

1. Approve canonical route strategy and compatibility window.
2. Implement auth compatibility endpoints and verify-email dual flow.
3. Decide OAuth implementation path (real provider integration vs deferred explicit disable).
4. Start AI hardening design for Cloudinary persistence + usage + credits.
