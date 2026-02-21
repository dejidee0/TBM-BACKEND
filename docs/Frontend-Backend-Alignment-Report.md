# Frontend-Backend Alignment Report

Generated on: February 21, 2026  
Input docs: `docs/frontend doc.md`, `docs/TBM-API-Documentation.md`  
Output matrix: `docs/frontend-backend-alignment-matrix.csv`

This report presents two lenses:
- `Strict Contract Coverage`: all 111 rows in the frontend contract doc.
- `Current Integration Coverage`: only `Live` + `Local` frontend rows.

## Executive Summary

- Backend controller action methods: 116

Strict contract coverage (111 frontend rows):
- Implemented: 50
- Partially Implemented: 15
- Capability-backed (Implemented + Partially Implemented): 65
- Not Implemented: 46

Current integration coverage (Live + Local only, 11 rows):
- Capability-backed (Implemented + Partially Implemented): 11
- Not Implemented: 0

Live rows only (9):
- Capability-backed: 9
- Not Implemented: 0

Local rows only (2):
- Capability-backed: 2
- Not Implemented: 0

Frontend status distribution:
- Inferred: 36
- Live: 9
- Local: 2
- Mock: 64

Priority distribution:
- P0: 9
- P1: 31
- P2: 71

## Why Backend Count and Strict Coverage Differ

- `116` counts backend controller action methods.
- Strict coverage counts only frontend-documented contract rows and classifies each row as `Implemented`, `Partially Implemented`, or `Not Implemented`.

## Section Coverage (Strict Contract Lens)

| Section | Total | Implemented | Partial | Missing |
|---|---:|---:|---:|---:|
| 1) Authentication and Identity | 10 | 0 | 10 | 0 |
| 2) Catalog / Materials | 3 | 3 | 0 | 0 |
| 3) Cart and Checkout | 9 | 9 | 0 | 0 |
| 4) User Orders, Saved Items, and Designs | 16 | 16 | 0 | 0 |
| 5) User Dashboard and Account | 22 | 22 | 0 | 0 |
| 6) Vendor Portal | 22 | 0 | 0 | 22 |
| 7) Admin Portal | 29 | 0 | 5 | 24 |

## Live and Local Endpoint Readiness

| Frontend Status | Method | Frontend Path | Backend Match | Implementation | Priority | Notes |
|---|---|---|---|---|---|---|
| Live | GET | /auth/apple | /auth/apple | Partially Implemented | P0 | Endpoint is implemented in disabled mode (501 + actionable response) until OAuth provider config is enabled. |
| Live | GET | /auth/google | /auth/google | Partially Implemented | P0 | Endpoint is implemented in disabled mode (501 + actionable response) until OAuth provider config is enabled. |
| Live | GET | /Auth/me | /api/v1/Auth/me | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Live | POST | /Auth/forgot-password | /api/v1/Auth/forgot-password | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Live | POST | /Auth/login | /api/v1/Auth/login | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Live | POST | /Auth/register | /api/v1/Auth/register | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Live | POST | /Auth/resend-verification | /api/v1/Auth/resend-verification | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Live | POST | /Auth/verify-email | /api/v1/Auth/verify-email | Partially Implemented | P0 | Backend supports both token query and { email, code } body; ensure frontend handles unified response envelope. |
| Live | POST | /Auth/verify-email?token={token} | /api/v1/Auth/verify-email | Partially Implemented | P0 | Backend capability exists but path/prefix normalization is required. |
| Local | GET | /api/auth/me | /api/v1/Auth/me | Partially Implemented | P1 | Backend capability exists but path/prefix normalization is required. |
| Local | GET | /api/flooring | /api/flooring | Implemented | P1 | Method/path present as-is in backend. |

## P0 Alignment Gaps (Action First)

| Method | Frontend Path | Backend Match | Gap |
|---|---|---|---|
| GET | /auth/apple | /auth/apple | Endpoint is implemented in disabled mode (501 + actionable response) until OAuth provider config is enabled. |
| GET | /auth/google | /auth/google | Endpoint is implemented in disabled mode (501 + actionable response) until OAuth provider config is enabled. |
| GET | /Auth/me | /api/v1/Auth/me | Backend capability exists but path/prefix normalization is required. |
| POST | /Auth/forgot-password | /api/v1/Auth/forgot-password | Backend capability exists but path/prefix normalization is required. |
| POST | /Auth/login | /api/v1/Auth/login | Backend capability exists but path/prefix normalization is required. |
| POST | /Auth/register | /api/v1/Auth/register | Backend capability exists but path/prefix normalization is required. |
| POST | /Auth/resend-verification | /api/v1/Auth/resend-verification | Backend capability exists but path/prefix normalization is required. |
| POST | /Auth/verify-email | /api/v1/Auth/verify-email | Backend supports both token query and { email, code } body; ensure frontend handles unified response envelope. |
| POST | /Auth/verify-email?token={token} | /api/v1/Auth/verify-email | Backend capability exists but path/prefix normalization is required. |
