# TBM Figma-to-Backend Requirements Alignment

Generated on: 2026-04-06

## 1. Status

- Figma MCP is configured locally in `.vscode/mcp.json` and was successfully authenticated during this review.
- Figma source reviewed: `https://www.figma.com/design/LecIlerhL3NDfYBqa3pfNv/TBM?node-id=1-2&p=f&t=YXPPkUPKTggeHSTx-0`
- Constraint: the connected Figma Starter plan hit MCP rate limits after root metadata and the full-canvas screenshot were fetched.
- Result: section-level coverage is direct from Figma; some per-screen mapping inside large sections is inferred from the visible canvas, the existing frontend contract docs, and the current backend code.

## 2. Figma Sections Observed

The following product areas are directly visible on the Figma board:

| Figma section | What is clearly present in the design | Backend implication |
|---|---|---|
| `Auth Screens` | Splash, onboarding, sign in, sign up, forgot password, password reset/email confirmation | Auth, password reset, verification, session bootstrap |
| `Landing Page - Public` | Marketing home, navigation, materials, AI visualizer CTA, project CTA, about/contact, book consultation CTA, footer | Mostly static content plus catalog preview, contact, consultation lead capture |
| `Landing Page - Protected` | Authenticated desktop customer experience with catalog/app flows | Customer catalog, AI/design flows, saved items, checkout, orders, account, projects |
| `Mobile View - Landing Page` | Mobile marketing landing screens | Same contracts as public landing |
| `Mobile` | Mobile customer app screens including home and order detail/tracking style screens | Same customer contracts, but mobile-friendly payload completeness is important |
| `Vendor` | Vendor dashboard, orders, inventory, delivery, messages/notifications style screens | Vendor-only routes and vendor-specific response shapes |
| `Admin` | Admin dashboard, users, settings, financial/reporting style screens | Admin auth, dashboard, users, settings, logs, reports, analytics |

Screens directly observed from metadata, not just the canvas screenshot:

- `Splash`
- `Onboard`
- `Sign in`
- `Sign up`
- `Forgot password`
- `Password reset`
- `Home` (desktop public)
- `Home` (mobile customer)
- `Order Details` (mobile customer, with tracking history, shipping details, items, payment summary, invoice/support actions)

## 3. Global Backend Requirements

These rules need to hold across all Figma-backed surfaces.

### 3.1 Routing

- Customer/public canonical routes are under `/api/v1/*`.
- Vendor canonical routes are under `/api/v1/vendor/*`.
- Admin canonical routes are under `/api/v1/admin/*`.
- Legacy compatibility aliases exist for some customer and vendor flows, but they are not complete or consistent.
- Existing docs in `docs/Frontend-Backend-Alignment-Report.md` and `docs/frontend-backend-alignment-matrix.csv` are stale in some places and should not be treated as authoritative without code verification.

### 3.2 Authentication

- Protected customer, vendor, and admin APIs rely on bearer JWTs.
- `GET /api/v1/auth/me` returns a raw user object from claims, not an `ApiResponse<T>` envelope.
- Some account endpoints also expose `GET /api/v1/account/me` with a richer profile object.

### 3.3 Response shape consistency

The backend currently uses three patterns:

- `ApiResponse<T>` style:
  - `{ success, message, data, errors }`
- Flat success payloads:
  - `{ items, subtotal, total, ... }`
- Flat error payloads:
  - `{ success: false, message }`
  - `{ error }`

For Figma alignment, frontend-facing flows should standardize on one pattern per domain. Right now, the UI has to tolerate mixed envelopes.

### 3.4 Pagination

Pagination is not uniform today:

- Customer list endpoints usually return:
  - `{ data-or-items, pagination: { page, limit, total, totalPages, hasMore } }`
- Vendor list endpoints often return:
  - `{ items, total, page, pageSize }`
- Admin list endpoints vary by controller/service.

### 3.5 File upload requirements

The Figma flows imply multiple upload points. Current backend support:

- Avatar upload:
  - `POST /api/v1/account/avatar`
- AI room image upload:
  - `POST /api/v1/ai/upload-room`
- Design session photo upload:
  - `POST /api/v1/designs/sessions/{sessionId}/upload`
- Project documents:
  - `POST /api/v1/projects/{projectId}/documents`
- Project gallery images:
  - `POST /api/v1/projects/{projectId}/gallery`
- Vendor order import:
  - `POST /api/v1/vendor/orders/import`

### 3.6 Lookup data

Any Figma dropdowns or status chips should be driven from:

- `GET /api/v1/lookups`
- `GET /api/v1/lookups/brand-types`
- `GET /api/v1/lookups/product-types`
- `GET /api/v1/lookups/material-types`
- `GET /api/v1/lookups/quality-tiers`
- `GET /api/v1/lookups/order-statuses`
- `GET /api/v1/lookups/payment-statuses`
- `GET /api/v1/lookups/payment-methods`
- `GET /api/v1/lookups/design-session-tiers`
- `GET /api/v1/lookups/project-statuses`

## 4. Detailed Requirements By Product Area

### 4.1 Public landing and lead capture

What the Figma expects:

- Static marketing content
- Featured products/materials
- AI visualizer promotion
- Consultation CTA
- Contact flow
- Possibly a public design/project gallery

Backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Contact form submit | `POST /api/v1/contact` | `{ fullName, email, phoneNumber?, subject?, message }` | `{ accepted, referenceId }` | Implemented |
| Public projects gallery | `GET /api/v1/public/projects?page=&limit=&roomType=&search=&sort=` | query only | `{ projects, pagination }` | Implemented |
| Featured product preview | `GET /api/v1/products/featured?brandType=&limit=` | query only | product list | Implemented |
| Materials listing | `GET /api/v1/materials` | product filter query | materials list + pagination | Implemented |
| Flooring alias | `GET /api/v1/flooring` | `category, materialType, minPrice, maxPrice, sort, page, limit` | `{ products, pagination, filters }` | Implemented |

Required product decision:

- The Figma has a visible `Book Consultation` CTA and consultation-focused messaging, but there is no real consultation booking resource in the backend.
- The existing contact endpoint can capture a generic message, but it does not model:
  - preferred date/time
  - room type
  - budget
  - consultation status
  - assigned designer
  - booking confirmation

Required new contract if consultations are meant to be first-class:

```json
POST /api/v1/consultations
{
  "fullName": "Jane Doe",
  "email": "jane@example.com",
  "phoneNumber": "+2348012345678",
  "roomType": "kitchen",
  "preferredDate": "2026-04-10",
  "preferredTime": "14:00",
  "budgetRange": "500000-1500000",
  "notes": "Need a modern renovation consultation",
  "source": "public-landing"
}
```

```json
200 OK
{
  "success": true,
  "consultationId": "4d5c6b8d-5f5f-4ba6-a8be-1b2c3d4e5f67",
  "status": "PendingConfirmation",
  "scheduledAt": null,
  "message": "Consultation request received"
}
```

### 4.2 Auth and identity

What the Figma expects:

- Registration
- Login
- Forgot password
- Password reset confirmation
- Email verification / account activation messaging
- Session restoration for protected screens

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Register | `POST /api/v1/auth/register` | `{ email, password, confirmPassword, firstName, lastName, phoneNumber? }` | `ApiResponse<TokenResponseDto>` with `{ accessToken, refreshToken, expiresAt, user, cart?, cartMerge* }` | Implemented |
| Login | `POST /api/v1/auth/login` | `{ email, password }` | `ApiResponse<TokenResponseDto>` | Implemented |
| Refresh | `POST /api/v1/auth/refresh-token` | `{ refreshToken }` | `ApiResponse<TokenResponseDto>` | Implemented |
| Current user | `GET /api/v1/auth/me` | bearer token | `{ userId, email, name, roles }` | Implemented |
| Forgot password | `POST /api/v1/auth/forgot-password` | `{ email }` | `ApiResponse<bool>` | Implemented |
| Reset password | `POST /api/v1/auth/reset-password` | token-based DTO | `ApiResponse<bool>` | Implemented |
| Resend verification | `POST /api/v1/auth/resend-verification` | `{ email }` | `ApiResponse<bool>` | Implemented |
| Provider capability flags | `GET /api/v1/auth/providers` | none | `{ google: { enabled, implemented }, apple: { enabled, implemented } }` | Implemented |
| Google OAuth | `GET /api/v1/auth/google` and `/auth/google` | none | `501` actionable error | Partial |
| Apple OAuth | `GET /api/v1/auth/apple` and `/auth/apple` | none | `501` actionable error | Partial |

Important misalignment:

- The code does not currently support body-based verification with `{ email, code }`.
- The actual controller only supports the query-token flow:
  - `GET /api/v1/auth/verify-email?token=...`
  - `POST /api/v1/auth/verify-email?token=...`
- `VerifyEmailCodeDto` exists in the codebase but is not wired to the controller.

Required fix if the Figma/front-end uses code entry:

```json
POST /api/v1/auth/verify-email/code
{
  "email": "jane@example.com",
  "code": "483921"
}
```

```json
200 OK
{
  "success": true,
  "message": "Email verified successfully",
  "data": true,
  "errors": null
}
```

### 4.3 Catalog, materials, search, and discovery

What the Figma expects:

- Product discovery on public and protected home screens
- Category/filter chips
- Search
- Featured/latest sections
- Related products
- Product detail entry points

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Unified product list | `GET /api/v1/products` | `ProductFilterDto` query | paged product result | Implemented |
| Product detail | `GET /api/v1/products/{id}` | route only | product detail | Implemented |
| Product detail by slug | `GET /api/v1/products/slug/{slug}` | route only | product detail | Implemented |
| Related products | `GET /api/v1/products/{id}/related?limit=4` | route/query | product list | Implemented |
| Featured products | `GET /api/v1/products/featured?brandType=&limit=` | query only | product list | Implemented |
| Material alias list | `GET /api/v1/materials` | filter query | materials list | Implemented |
| Material alias detail | `GET /api/v1/materials/{idOrSlug}` | route only | material detail | Implemented |
| Flooring alias | `GET /api/v1/flooring` | filter query | `{ products, pagination, filters }` | Implemented |
| Categories | `GET /api/v1/categories` and related lookup routes | query/route | category data | Implemented |

Alignment note:

- The backend surface is strong here.
- The main thing to confirm with frontend is canonical route choice:
  - product routes
  - materials aliases
  - flooring aliases

### 4.4 Saved items, moodboards, and favorites

What the Figma expects:

- Wishlist/favorites
- Save to board/moodboard
- Buy all / add to cart from saved state

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Saved list | `GET /api/v1/saved?category=&search=&sortBy=&page=&limit=` | query only | `{ items, boards, pagination }` | Implemented |
| Save item | `POST /api/v1/saved` | `{ itemId }` | `{ success }` | Implemented |
| Delete saved item | `DELETE /api/v1/saved/{id}` | route only | `{ success }` | Implemented |
| Add saved item to cart | `POST /api/v1/saved/{id}/add-to-cart` | `{ quantity }` | `{ success, message }` | Implemented |
| Add to moodboard | `POST /api/v1/saved/{id}/add-to-moodboard` | `{ boardId? }` | `{ success, message }` | Implemented |
| Create board | `POST /api/v1/saved/create-board` | `{ itemIds, boardName }` | `{ success, boardId, boardName, itemCount }` | Implemented |
| Buy all | `POST /api/v1/saved/buy-all` | `{ itemIds }` | `{ success, total, itemCount }` | Implemented |

Implementation note:

- Saved and moodboard state is currently persisted through `UserDataStoreService`, not a fully relational domain.
- That is acceptable for the present Figma flows, but it matters for analytics, multi-device consistency, and admin reporting.

### 4.5 Cart and checkout

What the Figma expects:

- Cart summary
- Quantity updates
- Promo application
- Checkout address selection
- Payment initialization
- Payment verification
- Invoice access after order creation

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Get cart | `GET /api/v1/cart` | none | cart DTO | Implemented |
| Add cart item | `POST /api/v1/cart/items` or alias `/api/v1/cart/api/cart/add` | `{ productId, quantity }` | updated cart | Implemented |
| Update cart item | `PUT /api/v1/cart/items/{itemId}` | `{ quantity }` | updated cart | Implemented |
| Remove cart item | `DELETE /api/v1/cart/items/{itemId}` | none | success boolean | Implemented |
| Clear cart | `DELETE /api/v1/cart` | none | success boolean | Implemented |
| Merge cart | `POST /api/v1/cart/merge` | merge request DTO | merge result | Implemented |
| Apply promo | `POST /api/v1/cart/apply-promo` | `{ code }` | promo result | Implemented |
| Related products | `GET /api/v1/cart/related?limit=4` | query only | product list | Implemented |
| Checkout summary | `GET /api/v1/checkout?promoCode=` | query only | `{ items, subtotal, shipping, tax, discount, total, savedAddresses, defaultAddress }` | Implemented |
| Validate promo | `POST /api/v1/checkout/validate-promo` | `{ code }` | `{ success, code, discount, type, discountAmount, message }` | Implemented |
| Initialize payment | `POST /api/v1/checkout/payment` | `{ designSessionId?, delivery, payment, total, promoCode?, idempotencyKey? }` | `{ success, orderId, orderNumber, message, idempotent, paymentProvider, paymentReference, paymentStatus, authorizationUrl, accessCode, publicKey }` | Implemented |
| Verify Paystack payment | `GET /api/v1/checkout/payment/paystack/verify/{reference}` | route only | checkout payment result | Implemented |

Important checkout requirement:

- Figma checkout and payment flows should always send an idempotency key for payment creation.
- The backend supports:
  - `Idempotency-Key`
  - `X-Idempotency-Key`

Recommended request shape:

```json
POST /api/v1/checkout/payment
{
  "designSessionId": null,
  "delivery": {
    "fullName": "Jane Doe",
    "phone": "+2348012345678",
    "address": "12 Allen Avenue",
    "city": "Ikeja",
    "state": "Lagos",
    "notes": "Call before delivery",
    "customerNotes": "Deliver after 4PM"
  },
  "payment": {
    "method": "Paystack",
    "reference": null,
    "callbackUrl": "https://app.example.com/checkout/verify"
  },
  "total": 57675.00,
  "promoCode": "WELCOME10"
}
```

### 4.6 Orders, invoice, and tracking

What the Figma expects:

- Order list
- Order detail
- Invoice action
- Shipping details
- Item list
- Payment summary
- Tracking history/timeline
- Estimated arrival
- Support action

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| User orders | `GET /api/v1/orders/my-orders` | none | `ApiResponse<List<OrderDto>>` | Implemented |
| Compatibility order list | `GET /api/v1/orders` | none | same as above | Implemented |
| User order detail | `GET /api/v1/orders/{orderId}` | route only | `ApiResponse<OrderDto>` | Implemented |
| Invoice URL | `GET /api/v1/orders/{orderId}/invoice` | route only | `{ success, url }` | Implemented |
| Invoice document | `GET /api/v1/orders/{orderId}/invoice/document` | route only | invoice object | Implemented |
| Cancel order | `POST /api/v1/orders/{orderId}/cancel` | `{ reason }` | success result | Implemented |
| Dashboard tracking link | `GET /api/v1/dashboard/orders/{orderId}/tracking` | route only | `{ success, trackingUrl }` | Partial |

Critical Figma mismatch:

- The mobile order detail design shows a real tracking history timeline.
- The customer API does not expose a first-class tracking history payload.
- `OrderDto` includes:
  - `status`
  - `trackingNumber`
  - `shippedAt`
  - `deliveredAt`
- It does not include:
  - ordered status-history items
  - milestone labels
  - milestone notes
  - ETA
  - carrier/method

Required new contract to match the Figma order timeline:

```json
GET /api/v1/orders/{orderId}/tracking
```

```json
200 OK
{
  "orderId": "3c7f6a6c-91d5-4015-af9f-1aeb6098f016",
  "orderNumber": "ORD202602210001",
  "currentStatus": "OnTheWay",
  "estimatedArrival": "2026-04-10T16:00:00Z",
  "carrier": "Standard Freight Delivery",
  "trackingNumber": "TBM1234567",
  "history": [
    {
      "status": "Processing",
      "label": "Order confirmed by TBM Admin",
      "note": "Order confirmed by TBM Admin",
      "occurredAt": "2026-04-06T09:15:00Z"
    },
    {
      "status": "Packaging",
      "label": "Quality check completed",
      "note": "Packaging complete",
      "occurredAt": "2026-04-07T13:30:00Z"
    },
    {
      "status": "OnTheWay",
      "label": "Departed from Regional Facility",
      "note": "Vehicle dispatched",
      "occurredAt": "2026-04-08T10:45:00Z"
    }
  ]
}
```

### 4.7 Dashboard and account

What the Figma expects:

- Recent order widget
- Latest design widget
- Saved items widget
- Consultation summary
- Profile and account editing
- Address book
- Password/security
- Notifications
- Brand access / role-aware shell
- Account deactivation/deletion
- Avatar upload

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Recent order widget | `GET /api/v1/dashboard/recent-order` | none | `{ hasOrder, orderId?, orderNumber?, status?, paymentStatus?, total?, createdAt? }` | Implemented |
| Latest design widget | `GET /api/v1/dashboard/latest-design` | none | `{ hasDesign, designId?, outputUrl?, outputType?, createdAt? }` | Implemented |
| Saved items widget | `GET /api/v1/dashboard/saved-items` | none | `{ totalSaved, latestSavedIds }` | Implemented |
| Consultations widget | `GET /api/v1/dashboard/consultations` | none | `{ upcomingCount, completedCount, nextConsultation }` | Partial |
| Profile | `GET /api/v1/account/profile` | none | profile object | Implemented |
| Rich profile | `GET /api/v1/account/me` | none | `{ success, data }` | Implemented |
| Update me | `PATCH /api/v1/account/me` | `{ firstName?, lastName?, phoneNumber?, email? }` | `{ success, data }` | Implemented |
| Update profile | `PUT /api/v1/account/profile` | `{ firstName?, lastName?, phoneNumber? }` | `{ success, data }` | Implemented |
| Update email | `PUT /api/v1/account/email` | `{ email }` | `{ success, message }` | Implemented |
| Update phone | `PUT /api/v1/account/phone` | `{ phone }` | `{ success, message }` | Implemented |
| Add address | `POST /api/v1/account/addresses` | address object | `{ success, data }` | Implemented |
| Update address | `PUT /api/v1/account/addresses/{addressId}` | address object | `{ success }` | Implemented |
| Delete address | `DELETE /api/v1/account/addresses/{addressId}` | none | `{ success }` | Implemented |
| Set default address | `PUT /api/v1/account/addresses/{addressId}/default` | none | `{ success }` | Implemented |
| Update password | `PUT /api/v1/account/password` | password DTO | `{ success, message }` | Implemented |
| Password OTP request | `POST /api/v1/account/password/otp/request` | OTP request | success result | Implemented |
| Password OTP verify | `POST /api/v1/account/password/otp/verify` | OTP verify request | success result | Implemented |
| Security state | `GET /api/v1/account/security` | none | security state | Implemented |
| Toggle 2FA | `PUT /api/v1/account/security/2fa` | `{ enabled }` | `{ success }` | Implemented |
| Notifications | `GET/PUT /api/v1/account/notifications` | state object on update | notification prefs | Implemented |
| Brand access | `GET /api/v1/account/brand-access` | none | `{ roles, canUseStore, canUseAdmin }` | Implemented |
| Deactivate account | `POST /api/v1/account/deactivate` | optional password | `{ success, message }` | Implemented |
| Delete account | `DELETE /api/v1/account` | optional password | `{ success, message }` | Implemented |
| Avatar upload | `POST /api/v1/account/avatar` | multipart file | `{ success, avatarUrl }` | Implemented |

Critical dashboard mismatch:

- `GET /api/v1/dashboard/consultations` currently returns hardcoded placeholder counts and `nextConsultation = null`.
- If the Figma dashboard shows live consultation data, the backend is not aligned today.

### 4.8 AI generation, design sessions, BOM, and projects

What the Figma expects:

- Upload a room photo
- Generate AI room variations
- Browse generated designs
- Favorite, download, share, and possibly publish them
- Guided premium design flow with BOM output
- Project follow-through after design selection

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Upload room photo | `POST /api/v1/ai/upload-room` | multipart file | `{ success, imageUrl }` | Implemented |
| Create AI project | `POST /api/v1/ai/projects` | `{ sourceImageUrl, outputType, prompt, contextLabel?, generationType? }` | AI project response DTO | Implemented |
| List AI projects | `GET /api/v1/ai/projects` | none | project list | Implemented |
| Generate image | `POST /api/v1/ai/generate/image` | `{ projectId, prompt?, sourceImageUrl? }` | generation result DTO | Implemented |
| Transform image | `POST /api/v1/ai/transform/image` | same as above | generation result DTO | Implemented |
| Generate video | `POST /api/v1/ai/generate/video` | `{ projectId, prompt?, sourceImageUrl?, durationSeconds }` | generation result DTO | Implemented |
| AI usage summary | `GET /api/v1/ai/usage/summary` | optional `year`, `month` | summary DTO | Implemented |
| Credit balance | `GET /api/v1/ai/credits/balance` | none | balance DTO | Implemented |
| Renovation estimate create | `POST /api/v1/ai/renovation/estimate` | estimator DTO | detailed estimate DTO | Implemented |
| Renovation estimate history | `GET /api/v1/ai/renovation/estimates` | none | `{ estimates }` | Implemented |
| Renovation estimate detail | `GET /api/v1/ai/renovation/estimates/{estimateId}` | route only | estimate DTO | Implemented |
| Design library list | `GET /api/v1/designs` | `roomType, search, sortBy, page, limit` | `{ designs, pagination }` | Implemented |
| Design detail | `GET /api/v1/designs/{id}` | route only | detail object | Implemented |
| Toggle favorite | `POST /api/v1/designs/{id}/favorite` | none | `{ success, isFavorite }` | Implemented |
| Delete design | `DELETE /api/v1/designs/{id}` | route only | `{ success }` | Implemented |
| Download design | `GET /api/v1/designs/{id}/download?quality=` | query only | `{ success, downloadUrl }` | Implemented |
| Share design | `POST /api/v1/designs/{id}/share` | none | `{ success, shareUrl }` | Implemented |
| Publish/unpublish design | `PATCH /api/v1/designs/{id}/visibility` | `{ isPublic }` | `{ success, designId, isPublic, publishedAt }` | Implemented |
| Public published designs | `GET /api/v1/public/projects` | query only | `{ projects, pagination }` | Implemented |

Design-session/BOM flow:

| Step | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Create session | `POST /api/v1/designs/sessions` | `{ projectName, roomType, visionText, tier, roomDimensions }` | `{ sessionId, sessionNumber, status, uploadUrl }` | Implemented |
| Upload design photo | `POST /api/v1/designs/sessions/{sessionId}/upload` | multipart image | `{ originalImageUrl, status }` | Implemented |
| Start generation | `POST /api/v1/designs/sessions/{sessionId}/generate` | `{ generateBOM: true }` | `{ status, estimatedTime, statusUrl }` | Implemented |
| Poll status | `GET /api/v1/designs/sessions/{sessionId}/status` | route only | `{ status, progress, currentStep, imageUrl, bomGenerated, errorMessage }` | Implemented |
| Session detail | `GET /api/v1/designs/sessions/{sessionId}` | route only | `{ session, billOfMaterials, allItemsInStock }` | Implemented |
| List sessions | `GET /api/v1/designs/sessions` | none | `{ sessions }` | Implemented |
| Add BOM/session to cart | `POST /api/v1/designs/sessions/{sessionId}/add-to-cart` | `{ addAll, itemIds }` | `{ cartId, itemsAdded, totalAmount }` | Implemented |

Project follow-through:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| User projects | `GET /api/v1/projects` | none | `{ projects }` | Implemented |
| Project detail | `GET /api/v1/projects/{projectId}` | route only | `{ project, timeline, materials, financial, vendor }` | Implemented |
| Timeline | `GET /api/v1/projects/{projectId}/timeline` | route only | `{ milestones }` | Implemented |
| Documents | `GET /api/v1/projects/{projectId}/documents` | route only | `{ documents }` | Implemented |
| Upload document | `POST /api/v1/projects/{projectId}/documents` | multipart | `{ documentId, fileUrl }` | Implemented |
| Gallery | `GET /api/v1/projects/{projectId}/gallery` | route only | `{ images }` | Implemented |
| Upload gallery image | `POST /api/v1/projects/{projectId}/gallery` | multipart | `{ imageId, imageUrl }` | Implemented |

Important design-flow mismatch:

- `POST /api/v1/designs/sessions/{sessionId}/generate` rejects `{ generateBOM: false }`.
- If the Figma product flow allows a design-only session without BOM generation, the backend is stricter than the UI.

### 4.9 Vendor portal

What the Figma expects:

- Vendor dashboard cards
- Alerts and activity feed
- Order queue/detail and status updates
- Delivery assignment
- Inventory stats/list/create/edit/delete
- Messages and notifications

Canonical backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Dashboard | `GET /api/v1/vendor/dashboard` | none | `VendorDashboardDto` | Implemented |
| Alerts | `GET /api/v1/vendor/alerts` | none | `List<VendorAlertDto>` | Implemented |
| Activity | `GET /api/v1/vendor/activity?filter=&page=&pageSize=` | query only | `VendorPagedResultDto<VendorActivityDto>` | Implemented |
| Orders list | `GET /api/v1/vendor/orders` | `page, pageSize, status, search, fromDate, toDate, assignedOnly, type` | paged order list | Implemented |
| Order export | `GET /api/v1/vendor/orders/export` | order filters | export metadata + base64 content | Implemented |
| Order import | `POST /api/v1/vendor/orders/import` | multipart file | import result | Implemented |
| Order detail | `GET /api/v1/vendor/orders/{orderId}` | route only | `VendorOrderDetailDto` | Implemented |
| Update order status | `PATCH /api/v1/vendor/orders/{orderId}/status` | `{ status, note? }` | `{ success }` | Implemented |
| Add note | `POST /api/v1/vendor/orders/{orderId}/notes` | `{ note }` | note DTO | Implemented |
| Delivery assignment | `PATCH /api/v1/vendor/orders/{orderId}/assignment` | `{ deliveryAgentName?, deliveryAgentPhone?, assignmentNote? }` | assignment result | Implemented |
| Inventory list | `GET /api/v1/vendor/inventory?page=&pageSize=&search=&lowStockOnly=` | query only | `VendorPagedResultDto<VendorInventoryItemDto>` | Implemented |
| Inventory stats | `GET /api/v1/inventory/stats` or `GET /api/v1/vendor/inventory/stats` | none | `VendorInventoryStatsDto` | Implemented |
| Create inventory product | `POST /api/v1/inventory/products` or `POST /api/v1/vendor/inventory/products` | `VendorInventoryCreateRequest` | created inventory result | Implemented |
| Update inventory | `PUT /api/v1/vendor/inventory/{productId}` | `VendorInventoryUpdateRequest` | updated inventory result | Implemented |
| Delete inventory product | `DELETE /api/v1/inventory/products/{productId}` | route only | `{ success }` | Implemented |
| Deliveries | `GET /api/v1/vendor/deliveries` | `page, pageSize, status` | paged deliveries | Implemented |
| Messages | `GET /api/v1/vendor/messages` and `POST /api/v1/vendor/messages` | query or `{ subject, body, to? }` | paged messages / created message | Implemented |
| Notifications | `GET /api/v1/vendor/notifications` | `page, pageSize, unreadOnly` | paged notifications | Implemented |
| Mark notification read | `PATCH /api/v1/vendor/notifications/{notificationId}/read` | none | `{ success }` | Implemented |
| Mark all read | `PUT /api/v1/notifications/mark-all-read` | none | `{ success, markedCount }` | Implemented |

Vendor-facing mismatches to resolve:

- Frontend docs still describe generic routes like:
  - `/api/orders`
  - `/api/messages/conversations`
  - `/api/notifications`
  - `/api/delivery/assignments`
- The backend canonical surface is vendor-scoped under `/api/v1/vendor/*`.
- Some method mismatches still exist:
  - frontend docs expect `PUT /api/orders/:id/status`
  - backend uses `PATCH /api/v1/vendor/orders/{orderId}/status`
- Some shape mismatches still exist:
  - frontend docs expect `{ products, pagination, stats }`
  - backend inventory returns `{ items, total, page, pageSize }`

Important vendor-risk note:

- `VendorDomainService.GetOrdersAsync` currently falls back to showing all orders when a vendor has no product ownership and no assignments.
- That behavior is risky for a scoped vendor portal and should be reviewed before production alignment is signed off.

### 4.10 Admin portal

What the Figma expects:

- Admin login
- Overview dashboard
- User management
- Orders and refunds
- Pricing and discount management
- Settings
- System logs
- Financial reporting
- Analytics
- AI/usage oversight
- Observability

Current backend contracts:

| Capability | Endpoint | Request payload | Response payload | Alignment |
|---|---|---|---|---|
| Admin login | `POST /api/v1/admin/auth/login` | `AdminLoginDto` | auth result | Implemented |
| Admin refresh | `POST /api/v1/admin/auth/refresh` | refresh DTO | auth result | Implemented |
| Admin logout | `POST /api/v1/admin/auth/logout` | refresh DTO | logout result | Implemented |
| Dashboard stats | `GET /api/v1/admin/dashboard/stats` | none | `AdminDashboardStatsDto` | Implemented |
| Dashboard revenue | `GET /api/v1/admin/dashboard/revenue?timeRange=` | query only | `AdminDashboardRevenueDto` | Implemented |
| Dashboard server load | `GET /api/v1/admin/dashboard/server-load` | none | `AdminDashboardServerLoadDto` | Implemented |
| Dashboard alerts | `GET /api/v1/admin/dashboard/alerts` | none | alert list | Implemented |
| Dashboard quick actions | `GET /api/v1/admin/dashboard/quick-actions` | none | action list | Implemented |
| Dashboard refresh | `POST /api/v1/admin/dashboard/refresh` | none | success payload | Implemented |
| Dashboard export | `POST /api/v1/admin/dashboard/export` | none | `{ success, filename, contentType, sizeBytes }` | Implemented |
| Users | `GET/POST/PATCH/DELETE /api/v1/admin/users*` | admin user DTOs | users list/detail/update results | Implemented |
| Vendors | `POST /api/v1/admin/vendors/{userId}/activate`, ownership/product assignment routes | admin vendor DTOs | activation/ownership results | Implemented |
| Orders | `/api/v1/admin/orders*` | query and order update DTOs | order admin results | Implemented |
| Settings | `/api/v1/admin/settings*` | settings DTOs | settings objects | Implemented |
| Discounts | `/api/v1/admin/admindiscounts*` | discount DTOs | discount objects | Implemented |
| Pricing | `/api/v1/admin/adminpricing*` | pricing DTOs | pricing objects | Implemented |
| Analytics | `/api/v1/admin/analytics*` | query only | analytics DTOs | Implemented |
| Financial | `/api/v1/admin/financial*` | query only | financial DTOs | Implemented |
| System logs | `/api/v1/admin/system-logs*` | query only | logs/stats/export | Implemented |
| AI admin | `/api/v1/admin/ai*` | query or adjustment DTOs | AI credits/usage data | Implemented |
| Observability | `/api/v1/admin/observability*` | query only | SLO data | Implemented |

Critical admin mismatches:

- The real route prefix is `/api/v1/admin/*`.
- Existing frontend/backlog docs still reference `/api/admin/*`.
- I did not find admin compatibility aliases in controllers.
- `AdminDashboardService.GetQuickActionsAsync()` currently returns routes like `/api/admin/users`, which do not match the actual controller prefix.
- `MaintenanceMiddleware` and admin-specific rate-limit path detection also look for `/api/admin`, not `/api/v1/admin`.

Operational data-quality note:

- Admin server-load and average-latency values are derived from the current process and GC data.
- They are not true cluster/APM metrics.
- If the Figma admin dashboard is meant to represent live infrastructure telemetry, this contract is functionally aligned but semantically approximate.

## 5. Highest-Risk Alignment Gaps

These are the items most likely to break the designed experience.

| Priority | Gap | Why it matters |
|---|---|---|
| P0 | No first-class consultation domain | The Figma visibly promotes consultation booking and dashboard consultation summaries, but the backend only has contact messaging plus a placeholder dashboard count |
| P0 | Customer tracking timeline contract is missing | The Figma order-detail screen shows tracking history; backend only exposes a tracking URL and basic order fields |
| P0 | Admin route prefix mismatch | Actual controllers use `/api/v1/admin/*`, but existing docs and some generated routes still assume `/api/admin/*` |
| P0 | Email verification body flow missing | UI/docs mention `{ email, code }`, but controller only accepts query-token verification |
| P1 | OAuth buttons are not backed | Google and Apple endpoints return `501 Not Implemented` |
| P1 | Vendor route/method/shape mismatches remain | Current vendor UI contract and backend canonical routes are not fully one-to-one |
| P1 | Dashboard consultations endpoint is placeholder data | Designed widgets will not show real data until consultation records exist |
| P1 | Admin quick-action routes are wrong | The returned quick-action URLs use `/api/admin/*` instead of `/api/v1/admin/*` |
| P1 | Vendor order visibility fallback is too broad | A vendor without ownership/assignments can currently see all orders |
| P2 | Design-session generation requires BOM | If the UI supports design-only generation, the backend will reject it |
| P2 | Admin telemetry is approximate | Good enough for placeholder cards, not good enough for a high-trust operations dashboard |

## 6. Recommended Backend Acceptance Checklist

Before declaring the backend aligned to this Figma, the following should be true:

1. Decide whether consultation is a real product domain.
2. If yes, add `consultations` endpoints and replace placeholder dashboard counts.
3. Add a customer-facing order tracking timeline endpoint, or expand `GET /api/v1/orders/{orderId}` to include ordered status history.
4. Choose a single admin prefix strategy:
   - either add `/api/admin/*` aliases
   - or migrate every consumer and generated quick-action route to `/api/v1/admin/*`
5. Either implement email-code verification or remove that expectation from UI/docs.
6. Hide or implement Google/Apple OAuth in any screen that surfaces those actions.
7. Normalize vendor endpoints to the Figma/frontend contract, especially for:
   - orders
   - inventory
   - delivery assignments
   - messages/notifications
8. Review vendor order-visibility rules so an unassigned vendor cannot see all orders by default.
9. Standardize response envelopes for all Figma-critical flows:
   - auth
   - checkout
   - orders
   - vendor
   - admin
10. Add contract tests for the Figma-critical user journeys:
    - register/login/me
    - forgot/reset/verify
    - contact/consultation
    - browse -> save -> cart -> checkout -> invoice
    - AI generate -> design library -> share/download
    - project detail and BOM add-to-cart
    - vendor order status update and inventory update
    - admin dashboard/users/settings

## 7. Bottom Line

The backend is broadly capable of supporting the TBM Figma product, but it is not fully aligned end-to-end yet.

Most customer commerce, AI, project, saved-item, account, vendor, and admin capabilities already exist.

The biggest blockers between the current backend and the designed experience are:

- consultation booking/tracking not being modeled as a real domain
- missing customer order timeline payloads
- admin route-prefix inconsistencies
- vendor contract mismatches
- stale docs that overstate current route compatibility
