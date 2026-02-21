# Full Platform API Endpoints (User + Vendor + Admin)

Last updated: February 20, 2026
Source: Current frontend codebase (`app/`, `hooks/`, `lib/actions/`, `lib/api/`, `lib/mock/`)

This document replaces the old admin-only spec with a full-platform contract for backend integration.

## Integration Status

- `Live`: Frontend currently performs an HTTP call to this endpoint.
- `Local`: Implemented as a Next.js route in this repo.
- `Mock`: UI currently uses mock modules, but this is the expected backend endpoint.
- `Inferred`: No explicit path in code; inferred from hook/service method names.

## Base URLs and Auth

- External backend base URL: `process.env.NEXT_PUBLIC_API_URL`
- Local Next.js routes used by frontend: `/api/*`
- Auth for protected endpoints:
  - Header: `Authorization: Bearer <token>`
  - Some flows also use cookie `authToken` (HTTP-only) set by server actions.

## 1) Authentication and Identity

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| POST | `/Auth/register` | Live | `{ email, firstName, lastName, phoneNumber, password, confirmPassword }` | frontend checks `response.ok`, error `message` on failure |
| POST | `/Auth/login` | Live | `{ email, password }` | `{ token, user }` |
| GET | `/Auth/me` | Live | Bearer token | user object (either raw object or `{ user }`) |
| POST | `/Auth/forgot-password` | Live | `{ email }` | success + optional `message` |
| POST | `/Auth/verify-email` | Live | Variant A: `{ email, code }` | success + optional `message` |
| POST | `/Auth/verify-email?token={token}` | Live | Variant B: token in query string | success + optional `message` |
| POST | `/Auth/resend-verification` | Live | `{ email }` | success + optional `message` |
| GET | `/auth/google` | Live | none (redirect flow) | OAuth redirect |
| GET | `/auth/apple` | Live | none (redirect flow) | OAuth redirect |
| GET | `/api/auth/me` | Local | frontend call without body; route reads cookie and proxies to `/Auth/me` | user JSON or `401 { error }` |

## 2) Catalog / Materials

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/flooring` | Local | Query: `category`, `materialType`, `minPrice`, `maxPrice`, `sort`, `page`, `limit` | `{ products, pagination, filters }` |
| GET | `/materials` | Inferred | Query expected similar to flooring list | material list |
| GET | `/materials/:id` | Inferred | path `id` | material detail |

## 3) Cart and Checkout

### 3.1 Cart

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/cart` | Mock | none | `{ items, subtotal, shipping, taxRate, estimatedDelivery }` |
| POST | `/api/cart/add` | Mock | `{ productId, quantity }` | `{ success, item?, message? }` |
| PUT | `/api/cart/items/:itemId` | Mock | `{ quantity }` | `{ success, item }` |
| DELETE | `/api/cart/items/:itemId` | Mock | none | `{ success }` |
| POST | `/api/cart/apply-promo` | Mock | `{ code }` | `{ success, code, discount, type }` |
| GET | `/api/cart/related` | Mock | none | `[{ id, name, price, image, rating }]` |

### 3.2 Checkout

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/checkout` | Inferred | none | `{ items, subtotal, shipping, tax, total, savedAddresses, defaultAddress }` |
| POST | `/api/checkout/payment` | Inferred | `{ delivery, payment, items, total }` | `{ success, orderId, message? }` |
| POST | `/api/checkout/validate-promo` | Inferred | `{ code }` | `{ success, code, discount, type }` |

## 4) User Orders, Saved Items, and Designs

### 4.1 User Orders

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/orders` | Mock | Query: `search`, `status`, `dateRange`, `sortBy` (user dashboard flow) | user order list |
| GET | `/api/orders/:orderId` | Mock | path `orderId` | order detail object |
| GET | `/api/orders/:orderId/invoice` | Mock | path `orderId` | `{ success, url }` |

### 4.2 Saved Items / Moodboards

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/saved` | Mock | Query: `category`, `search`, `sortBy`, `page`, `limit` | saved items array |
| POST | `/api/saved` | Inferred | `{ itemId }` | `{ success }` |
| DELETE | `/api/saved/:id` | Mock | path `id` | `{ success }` |
| POST | `/api/saved/:id/add-to-cart` | Mock | `{ quantity }` | `{ success, message }` |
| POST | `/api/saved/:id/add-to-moodboard` | Inferred | `{ boardId? }` | `{ success, message }` |
| POST | `/api/saved/create-board` | Mock | `{ itemIds, boardName }` | `{ success, boardId, boardName, itemCount }` |
| POST | `/api/saved/buy-all` | Mock | `{ itemIds }` | `{ success, total, itemCount }` |

### 4.3 AI Designs

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/designs` | Mock | Query: `roomType`, `search`, `sortBy`, `page`, `limit` | designs list |
| GET | `/api/designs/:id` | Mock | path `id` | design detail |
| POST | `/api/designs/:id/favorite` | Mock | none | `{ success, isFavorite }` |
| DELETE | `/api/designs/:id` | Mock | path `id` | `{ success }` |
| GET | `/api/designs/:id/download` | Mock | Query: `quality=standard|high` | `{ success, downloadUrl }` |
| POST | `/api/designs/:id/share` | Mock | none | `{ success, shareUrl }` |

## 5) User Dashboard and Account

### 5.1 Dashboard Widgets

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/dashboard/recent-order` | Inferred | none | recent order widget data |
| GET | `/api/dashboard/latest-design` | Inferred | none | latest design widget data |
| GET | `/api/dashboard/consultations` | Inferred | none | consultation summary |
| GET | `/api/dashboard/saved-items` | Inferred | none | saved items preview |
| GET | `/api/dashboard/orders/:orderId/tracking` | Inferred | path `orderId` | `{ success, trackingUrl }` |

### 5.2 Account / Profile

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/account/profile` | Mock | none | profile object |
| PUT | `/api/account/profile` | Mock | partial profile fields | `{ success, data|profile }` |
| PUT | `/api/account/email` | Inferred | `{ email }` | `{ success, message }` |
| PUT | `/api/account/phone` | Inferred | `{ phone }` | `{ success, message }` |
| POST | `/api/account/addresses` | Inferred | address object | `{ success, data }` |
| PUT | `/api/account/addresses/:addressId` | Inferred | partial address object | `{ success }` |
| DELETE | `/api/account/addresses/:addressId` | Inferred | none | `{ success }` |
| PUT | `/api/account/addresses/:addressId/default` | Inferred | none | `{ success }` |
| PUT | `/api/account/password` | Mock | `{ currentPassword, newPassword }` | `{ success, message? }` |
| GET | `/api/account/security` | Mock | none | `{ twoFactorEnabled }` |
| PUT | `/api/account/security/2fa` | Mock | `{ enabled }` | `{ success }` |
| GET | `/api/account/notifications` | Mock | none | notification prefs object |
| PUT | `/api/account/notifications` | Mock | notification prefs object | `{ success }` |
| GET | `/api/account/brand-access` | Mock | none | brand access array |
| POST | `/api/account/deactivate` | Mock | optional `{ password }` | `{ success, message? }` |
| DELETE | `/api/account` | Inferred | `{ password }` | `{ success, message }` |
| POST | `/api/account/avatar` | Inferred | multipart file upload | `{ success, avatarUrl }` |

## 6) Vendor Portal

### 6.1 Vendor Dashboard

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/vendor/dashboard/stats` | Mock | none | stats cards data |
| GET | `/api/vendor/dashboard/alerts` | Mock | none | operational alerts |
| GET | `/api/vendor/dashboard/activity` | Mock | Query: `filter` | recent activity list |

### 6.2 Vendor Orders and Fulfillment

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/orders` | Mock | Query: `page`, `limit`, `status`, `type`, `dateRange`, `search` | vendor orders list + pagination |
| GET | `/api/orders/:id` | Mock | path `id` | vendor order detail |
| PUT | `/api/orders/:id/status` | Mock | `{ status }` | `{ success, status }` |
| POST | `/api/orders/:id/notes` | Mock | `{ notes }` | `{ success, notes }` |
| POST | `/api/orders/:id/assign-delivery` | Mock | `{ carrier, trackingNumber }` | `{ success, carrier, trackingNumber }` |

### 6.3 Inventory

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/inventory/stats` | Mock | none | inventory stat cards |
| GET | `/api/inventory/products` | Mock | Query: `page`, `limit`, `search`, `stockStatus`, `location`, `archived` | `{ products, pagination, stats }` |
| PUT | `/api/inventory/products/:id/quantity` | Mock | `{ quantity }` | `{ success, quantity }` |
| DELETE | `/api/inventory/products/:id` | Mock | none | `{ success }` |
| POST | `/api/inventory/products` | Inferred | add product modal payload | created product |
| PUT | `/api/inventory/products/:id` | Inferred | edit product payload | updated product |

### 6.4 Delivery Assignments

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/delivery/assignments` | Mock | Query: `page`, `limit`, `search`, `status`, `dateRange` | `{ assignments, pagination }` |
| PUT | `/api/delivery/assignments/:id` | Mock | `{ deliveryPartner, trackingNumber, ... }` | `{ success, assignment }` |

### 6.5 Vendor Messages and Notifications

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/messages/conversations` | Mock | Query: `filter`, `search` | `{ conversations, counts }` |
| GET | `/api/messages/:conversationId` | Mock | path `conversationId` | messages array |
| POST | `/api/messages/:conversationId` | Mock | `{ message }` | created message |
| GET | `/api/notifications` | Mock | Query: `category`, `search` | `{ notifications, unreadCount, total }` |
| PUT | `/api/notifications/mark-all-read` | Mock | none | `{ success }` |
| PUT | `/api/notifications/:id/read` | Mock | path `id` | `{ success }` |

## 7) Admin Portal

### 7.1 Admin Dashboard Overview

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/dashboard/stats` | Inferred | none | `{ platformUptime, activeUsers, avgApiLatency }` |
| GET | `/api/admin/dashboard/revenue` | Inferred | Query: `timeRange` | `{ totalRevenue, monthlyRecurring, chartData, timeRange }` |
| GET | `/api/admin/dashboard/server-load` | Inferred | none | `{ cluster, capacity, status, cpuUsage, memoryUsage, diskUsage }` |
| GET | `/api/admin/dashboard/alerts` | Inferred | none | alert rows |
| GET | `/api/admin/dashboard/quick-actions` | Inferred | none | quick action list |
| POST | `/api/admin/dashboard/refresh` | Inferred | none | `{ success, message }` |
| POST | `/api/admin/dashboard/export` | Inferred | none | `{ success, filename }` |

### 7.2 Admin User and Role Management

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/users` | Mock | Query: `page`, `limit`, `search`, `role`, `status` | `{ users, pagination }` |
| GET | `/api/admin/users/:id` | Mock | path `id` | single user |
| POST | `/api/admin/users` | Inferred | `{ fullName, email, role, isActive, password }` | created user |
| PATCH | `/api/admin/users/:id/status` | Inferred | toggle active/inactive | `{ success, user }` |
| PATCH | `/api/admin/users/:id/role` | Inferred | `{ newRole }` | `{ success, user }` |
| DELETE | `/api/admin/users/:id` | Mock | none | `{ success }` |
| GET | `/api/admin/users/export` | Mock | none | `{ success, filename }` |

### 7.3 Admin Platform Settings

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/settings/payment` | Mock | none | `{ gateways, fees }` |
| GET | `/api/admin/settings/ai` | Mock | none | AI config object |
| GET | `/api/admin/settings/notifications` | Mock | none | notification config |
| GET | `/api/admin/settings/general` | Mock | none | general config |
| PUT | `/api/admin/settings` | Mock | settings payload from form (currently `{ baseFee, fixedFee, currency }`) | `{ success, message }` |
| PATCH | `/api/admin/settings/payment/gateways/:gatewayId` | Mock | `{ enabled }` | `{ success, gateway }` |
| PATCH | `/api/admin/settings/ai/models/:modelId` | Mock | `{ enabled }` | `{ success, model }` |

### 7.4 Admin System Logs

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/system-logs/stats` | Inferred | none | system stats cards |
| GET | `/api/admin/system-logs` | Mock | Query: `page`, `limit`, `search`, `severity`, `dateRange` | `{ logs, pagination }` |
| GET | `/api/admin/system-logs/export` | Mock | none | `{ success, filename }` |

### 7.5 Admin Financial Reports

| Method | Path | Status | Request (from frontend) | Response fields consumed |
| --- | --- | --- | --- | --- |
| GET | `/api/admin/financial/stats` | Inferred | none | financial stat cards |
| GET | `/api/admin/financial/monthly-revenue` | Inferred | none | monthly revenue series |
| GET | `/api/admin/financial/revenue-by-service` | Inferred | none | revenue split object |
| GET | `/api/admin/financial/transactions` | Mock | Query: `page`, `limit`, `search`, `filter` | `{ transactions, pagination }` |
| GET | `/api/admin/financial/export` | Mock | none | `{ success, filename }` |

## 8) Important Backend Decisions (from current code behavior)

1. `/api/orders` is used by both user and vendor flows with different response shapes.
   - Option A: same endpoint with role-aware response.
   - Option B: split into `/api/user/orders` and `/api/vendor/orders` and update frontend.

2. `/api/account/profile` is used in two contexts with different fields (customer profile vs vendor profile).
   - Current frontend can handle superset model.

3. Verify-email currently has two frontend call variants.
   - Keep both temporarily:
     - `POST /Auth/verify-email` with `{ email, code }`
     - `POST /Auth/verify-email?token=...`

4. Auth path casing is mixed in frontend (`/Auth/*` and `/auth/*`).
   - Backend should support current casing or frontend should normalize soon.

5. `app/(auth)/reset-email-sent/page.jsx` still contains placeholder `YOUR_API_ENDPOINT/auth/forgot-password`.
   - Treat as TODO in frontend; primary real flow uses `/Auth/forgot-password`.

## 9) Recommended Common Response Patterns

### Success

```json
{
  "success": true,
  "data": {}
}
```

### Error

```json
{
  "success": false,
  "message": "Human-readable error"
}
```

### Pagination

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "limit": 10,
    "total": 100,
    "totalPages": 10,
    "hasMore": true
  }
}
```

---

If backend wants, next step is converting this document into OpenAPI 3.1 so frontend and backend can validate contracts automatically.
