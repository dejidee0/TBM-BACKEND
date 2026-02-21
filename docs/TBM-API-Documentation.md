# TBM Digital Platform API Documentation

Generated from the current codebase snapshot on February 20, 2026.

This document was derived from:
- `src/TBM.API/Program.cs`
- `src/TBM.API/Controllers/**/*.cs`
- `src/TBM.Application/DTOs/**/*.cs`
- `src/TBM.Application/Interfaces/*.cs`
- `src/TBM.Application/Services/**/*.cs`
- `src/TBM.Core/Enums/*.cs`

Current API surface in controllers: 68 endpoints.

## 1. Platform Overview

- Primary API prefixes:
  - `/api/v1/*`
  - `/api/admin/*`
  - `/api/webhooks/*`
- Swagger UI is exposed at `/` (`RoutePrefix = string.Empty`).
- Swagger JSON is exposed at `/swagger/v1/swagger.json`.
- Route matching in ASP.NET Core is case-insensitive.

## 2. Security and Access Control

### 2.1 JWT Authentication

- Authentication scheme: `Bearer` JWT.
- Header format:
  - `Authorization: Bearer <access_token>`
- Token validation configured for issuer, audience, lifetime, and signing key.

### 2.2 Role-Based Authorization

Roles in code (`src/TBM.Core/Enums/UserRoles.cs`):
- `Customer`
- `Admin`
- `SuperAdmin`

Role protections used:
- `Authorize` (any authenticated user)
- `Authorize(Roles = "SuperAdmin")`
- `Authorize(Roles = "Admin,SuperAdmin")`

## 3. Global Middleware and Runtime Behavior

### 3.1 Rate Limiting

- Named policy: `DynamicPolicy`.
- Applied via `[EnableRateLimiting("DynamicPolicy")]` on selected controllers.
- Limit source: `GeneralSettingsDto.ApiRateLimit` from settings store.
- Fallback if missing: 1000 requests per minute.
- Partition key: authenticated username, else remote IP, else `anonymous`.
- 429 response on limit breach.

Rate-limited controller groups:
- `AuthController`
- `ProductsController`
- `CategoriesController`
- `CartController`
- `OrdersController`
- `AdminAuthController`
- `AdminSettingsController`

### 3.2 Maintenance Mode

`MaintenanceMiddleware` checks `GeneralSettingsDto.MaintenanceMode` and returns `503` for non-admin/non-swagger requests when enabled.

Always allowed while maintenance mode is active:
- Paths starting with `/api/admin`
- Paths starting with `/health`
- Paths starting with `/swagger`

Maintenance response shape:
```json
{
  "success": false,
  "error": {
    "code": "MAINTENANCE_MODE",
    "message": "Platform is temporarily under maintenance."
  }
}
```

### 3.3 CORS

- Policy name: `AllowAll`
- Allows any origin, method, and header.

## 4. Common Response Patterns

### 4.1 Standard Envelope (`ApiResponse<T>`)

Used by most `api/v1` business endpoints.

```json
{
  "success": true,
  "message": "Operation successful",
  "data": {},
  "errors": []
} 
```
 
Fields:
- `success`: `bool`
- `message`: `string`
- `data`: generic payload (`T`)
- `errors`: `List<string>` (typically present on failures)

### 4.2 Paged Wrapper Types

- `PagedResultDto<T>`:
  - `items`, `totalCount`, `pageNumber`, `pageSize`
- `PagedResult<T>`:
  - `items`, `totalCount`, `page`, `pageSize`

### 4.3 Non-Envelope Endpoints

These return raw DTO/entity/string objects instead of `ApiResponse<T>`:
- Most `/api/admin/*` endpoints (except `POST /api/admin/auth/logout`)
- All `/api/v1/ai/*` endpoints
- `POST /api/webhooks/paystack`
- `GET /api/v1/auth/me`
- `POST /api/v1/auth/logout`

## 5. Enum Reference

### 5.1 BrandType
- `1 = TBM`
- `2 = Bogat`

### 5.2 ProductType
- `1 = PhysicalProduct`
- `2 = Service`

### 5.3 OrderStatus
- `0 = Pending`
- `1 = PaymentReceived`
- `2 = Processing`
- `3 = Shipped`
- `4 = Delivered`
- `5 = Completed`
- `6 = Cancelled`
- `7 = Refunded`

### 5.4 PaymentStatus
- `1 = Pending`
- `2 = Paid`
- `3 = Failed`
- `4 = Refunded`
- `5 = PartiallyPaid`

### 5.5 PaymentMethod
- `1 = Paystack`
- `2 = Flutterwave`
- `3 = BankTransfer`
- `4 = Cash`

### 5.6 UserStatus
- `1 = Active`
- `2 = Suspended`
- `3 = Pending`

### 5.7 AIGenerationType
- `1 = ImageToImage`
- `2 = ImageToVideo`

### 5.8 AIOutputType
- `1 = Image`
- `2 = Video`

### 5.9 AIProjectStatus
- `1 = Pending`
- `2 = Processing`
- `3 = Completed`
- `4 = Failed`

## 6. Endpoint Reference

## 6.1 User Authentication (`/api/v1/auth`)

Controller: `src/TBM.API/Controllers/V1/AuthController.cs`

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Public | `RegisterDto` body | `ApiResponse<TokenResponseDto>` (200) | 400 with `ApiResponse` when registration fails |
| POST | `/api/v1/auth/login` | Public | `LoginDto` body | `ApiResponse<TokenResponseDto>` (200) | 400 on invalid credentials |
| POST | `/api/v1/auth/refresh-token` | Public | `RefreshTokenDto` body | `ApiResponse<TokenResponseDto>` (200) | 400 on invalid/expired refresh token |
| POST | `/api/v1/auth/forgot-password` | Public | `ForgotPasswordDto` body | `ApiResponse<bool>` (always 200) | Never reveals whether email exists |
| POST | `/api/v1/auth/reset-password` | Public | `ResetPasswordDto` body | `ApiResponse<bool>` (200) | 400 on password mismatch or invalid token |
| POST | `/api/v1/auth/verify-email?token=...` | Public | `token` query | `ApiResponse<bool>` (200) | 400 for invalid/expired token |
| POST | `/api/v1/auth/resend-verification` | Public | `ForgotPasswordDto` body (`email`) | `ApiResponse<bool>` (200) | Returns success envelope |
| GET | `/api/v1/auth/me` | JWT required | none | Raw object `{ userId, email, name, roles }` (200) | 401 if token missing/invalid |
| POST | `/api/v1/auth/logout` | JWT required | none | Raw object `{ message }` (200) | Stateless logout (client discards token) |

Forgot password link behavior (`AuthService`):
- Reset URL format:
  - `https://tdm-web.vercel.app/verify?purpose=forgot_password&token={token}&email={email}`
- Token and email are URL-encoded.
- Reset token expiry set to 1 hour.
- Successful password reset also triggers `SendPasswordResetConfirmationAsync`.

## 6.2 Admin Authentication (`/api/admin/auth`)

Controller: `src/TBM.API/Controllers/V1/Admin/AdminAuthController.cs`

| Method | Path | Auth | Request | Success Response | Notes |
|---|---|---|---|---|---|
| POST | `/api/admin/auth/login` | AllowAnonymous | `AdminLoginDto` body | `ApiResponse<TokenResponseDto>` in 200 | Admin-only role check is enforced in service (`Admin` or `SuperAdmin`) |
| POST | `/api/admin/auth/refresh` | AllowAnonymous | `RefreshTokenDto` body | `ApiResponse<TokenResponseDto>` in 200 | Uses same refresh flow as user auth |
| POST | `/api/admin/auth/logout` | `Admin` or `SuperAdmin` | `RefreshTokenDto` body | `ApiResponse<bool>` in 200 | Clears refresh token in DB if found |

## 6.3 Products (`/api/v1/products`)

Controller: `src/TBM.API/Controllers/V1/ProductsController.cs`

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| GET | `/api/v1/products` | Public | `ProductFilterDto` query | `ApiResponse<PagedResultDto<ProductDto>>` | 400 on service errors |
| GET | `/api/v1/products/featured` | Public | query: `brandType?`, `limit=10` | `ApiResponse<List<ProductDto>>` | 400 on service errors |
| GET | `/api/v1/products/{id}` | Public | `id` path | `ApiResponse<ProductDto>` | 404 when missing |
| GET | `/api/v1/products/slug/{slug}` | Public | `slug` path | `ApiResponse<ProductDto>` | 404 when missing |
| GET | `/api/v1/products/{id}/related` | Public | `id` path, `limit=4` query | `ApiResponse<List<ProductDto>>` | 400 on service errors |
| POST | `/api/v1/products` | `SuperAdmin` | `CreateProductDto` body | `ApiResponse<ProductDto>` with 201 | 400 on validation/business errors |
| PUT | `/api/v1/products/{id}` | `SuperAdmin` | `id` path + `UpdateProductDto` body | `ApiResponse<ProductDto>` | 400 on errors |
| DELETE | `/api/v1/products/{id}` | `SuperAdmin` | `id` path | `ApiResponse<bool>` | 400 on errors |
| POST | `/api/v1/products/{id}/images` | `SuperAdmin` | `id` path + `AddProductImageDto` body | `ApiResponse<ProductImageDto>` | 400 on errors |
| DELETE | `/api/v1/products/images/{imageId}` | `SuperAdmin` | `imageId` path | `ApiResponse<bool>` | 400 on errors |
| PUT | `/api/v1/products/{productId}/images/{imageId}/primary` | `SuperAdmin` | `productId`, `imageId` path | `ApiResponse<bool>` | 400 on errors |

## 6.4 Categories (`/api/v1/categories`)

Controller: `src/TBM.API/Controllers/V1/CategoriesController.cs`

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| GET | `/api/v1/categories` | Public | none | `ApiResponse<List<CategoryDto>>` | 400 on service errors |
| GET | `/api/v1/categories/brand/{brandType}` | Public | `brandType` path (`1` TBM, `2` Bogat) | `ApiResponse<List<CategoryDto>>` | 400 on errors |
| GET | `/api/v1/categories/{id}` | Public | `id` path | `ApiResponse<CategoryDto>` | 404 when missing |
| GET | `/api/v1/categories/slug/{slug}` | Public | `slug` path | `ApiResponse<CategoryDto>` | 404 when missing |
| POST | `/api/v1/categories` | `SuperAdmin` | `CreateCategoryDto` body | `ApiResponse<CategoryDto>` with 201 | 400 on errors |
| PUT | `/api/v1/categories/{id}` | `SuperAdmin` | `id` path + `UpdateCategoryDto` body | `ApiResponse<CategoryDto>` | 400 on errors |
| DELETE | `/api/v1/categories/{id}` | `SuperAdmin` | `id` path | `ApiResponse<bool>` | 400 on errors |

## 6.5 Cart (`/api/v1/cart`)

Controller: `src/TBM.API/Controllers/V1/CartController.cs`

All endpoints require JWT. `userId` is taken from `ClaimTypes.NameIdentifier`.

| Method | Path | Request | Success Response | Failure Behavior |
|---|---|---|---|---|
| GET | `/api/v1/cart` | none | `ApiResponse<CartDto>` | 400 on service errors |
| POST | `/api/v1/cart/items` | `AddToCartDto` body | `ApiResponse<CartDto>` | 400 on errors |
| PUT | `/api/v1/cart/items/{itemId}` | `itemId` path + `UpdateCartItemDto` body | `ApiResponse<CartDto>` | 400 on errors |
| DELETE | `/api/v1/cart/items/{itemId}` | `itemId` path | `ApiResponse<bool>` | 400 on errors |
| DELETE | `/api/v1/cart` | none | `ApiResponse<bool>` | 400 on errors |

## 6.6 Orders (`/api/v1/orders`)

Controller: `src/TBM.API/Controllers/V1/OrdersController.cs`

All endpoints require JWT. Some routes additionally require `SuperAdmin`.

### 6.6.1 Customer-Level

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| GET | `/api/v1/orders/my-orders` | Any authenticated user | none | `ApiResponse<List<OrderDto>>` | 400 on errors |
| GET | `/api/v1/orders/{orderId}` | Any authenticated user | `orderId` path | `ApiResponse<OrderDto>` | 404 when missing/not owned |
| GET | `/api/v1/orders/number/{orderNumber}` | Any authenticated user | `orderNumber` path | `ApiResponse<OrderDto>` | 404 when missing/not owned |
| POST | `/api/v1/orders` | Any authenticated user | `CreateOrderDto` body | `ApiResponse<OrderDto>` with 201 | 400 on errors |
| POST | `/api/v1/orders/{orderId}/cancel` | Any authenticated user | `orderId` path + `CancelOrderDto` body | `ApiResponse<bool>` | 400 on invalid cancellation |

### 6.6.2 SuperAdmin-Level

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| GET | `/api/v1/orders` | `SuperAdmin` | `OrderFilterDto` query | `ApiResponse<PagedResultDto<OrderDto>>` | 400 on errors |
| GET | `/api/v1/orders/admin/{orderId}` | `SuperAdmin` | `orderId` path | `ApiResponse<OrderDto>` | 404 when missing |
| PUT | `/api/v1/orders/{orderId}/status` | `SuperAdmin` | `UpdateOrderStatusDto` body | `ApiResponse<OrderDto>` | 400 on invalid transition/input |
| PUT | `/api/v1/orders/{orderId}/payment` | `SuperAdmin` | `UpdatePaymentStatusDto` body | `ApiResponse<OrderDto>` | 400 on invalid update |

Order status transitions enforced by `OrderStatusValidator`:
- `Pending -> Processing` or `Cancelled`
- `Processing -> Shipped` or `Cancelled`
- `Shipped -> Completed`
- `Completed -> Refunded`
- `Cancelled` and `Refunded` are terminal

## 6.7 AI (`/api/v1/ai`)

Controllers:
- `src/TBM.API/Controllers/V1/AI/AIController.cs`
- `src/TBM.API/Controllers/V1/AI/AIUploadController.cs`

All endpoints require JWT.

| Method | Path | Request | Success Response | Failure Behavior |
|---|---|---|---|---|
| POST | `/api/v1/ai/projects` | `CreateAIProjectDto` body | Raw `AIProject` entity | 401 if user claim missing |
| POST | `/api/v1/ai/generate/image` | `GenerateImageDto` body | Raw `AIDesign` entity | 401 if user claim missing, exception-driven 500 possible |
| POST | `/api/v1/ai/transform/image` | `GenerateImageDto` body | Raw `AIDesign` entity | 500 with `{ error }` on exception |
| POST | `/api/v1/ai/generate/video` | `GenerateVideoDto` body | Raw `AIDesign` entity | 500 with `{ error }` on exception |
| POST | `/api/v1/ai/upload-room` | `multipart/form-data` with `file` | Raw `{ success: true, imageUrl: string }` | 401 unauthorized, other upload errors bubble |

## 6.8 Admin Users (`/api/admin/users`)

Controller: `src/TBM.API/Controllers/V1/Admin/AdminUsersController.cs`

All endpoints require role `Admin` or `SuperAdmin`.

| Method | Path | Request | Success Response | Notes |
|---|---|---|---|---|
| GET | `/api/admin/users` | query: `page=1`, `pageSize=20`, `search?`, `role?`, `status?` | `PagedResult<AdminUserListDto>` | Raw response, no `ApiResponse` envelope |
| PATCH | `/api/admin/users/{id}/suspend` | `id` path | `"User suspended"` | Uses current admin ID from token |
| PATCH | `/api/admin/users/{id}/reactivate` | `id` path | `"User reactivated"` | Raw string response |
| DELETE | `/api/admin/users/{id}` | `id` path | `"User deleted"` | Soft delete semantics in service |

## 6.9 Admin Orders (`/api/admin/orders`)

Controller: `src/TBM.API/Controllers/V1/Admin/AdminOrdersController.cs`

All endpoints require role `Admin` or `SuperAdmin`.

| Method | Path | Request | Success Response | Notes |
|---|---|---|---|---|
| GET | `/api/admin/orders` | query: `page=1`, `pageSize=20`, `status?`, `search?` | `PagedResult<AdminOrderListDto>` | Raw response |
| PATCH | `/api/admin/orders/{id}/status` | `id` path + raw `OrderStatus` in body | `"Status updated"` | Valid transitions enforced |
| PATCH | `/api/admin/orders/{id}/cancel` | `id` path + raw string `reason` body | `"Order cancelled"` | Cancel reason persisted |
| POST | `/api/admin/orders/{id}/refund` | `id` path + raw string `reason` body | `"Order refunded"` | Calls Paystack refund API |
| PATCH | `/api/admin/orders/{id}/tracking` | `id` path + `TrackingDto` body | `"Tracking updated"` | Sets `TrackingNumber` and `ShippedAt` |

## 6.10 Admin Analytics (`/api/admin/analytics`)

Controller: `src/TBM.API/Controllers/V1/AdminAnalyticsController.cs`

All endpoints require role `Admin` or `SuperAdmin`.

| Method | Path | Success Response |
|---|---|---|
| GET | `/api/admin/analytics/overview` | `AdminAnalyticsOverviewDto` |
| GET | `/api/admin/analytics/monthly-revenue` | `List<MonthlyRevenueDto>` |
| GET | `/api/admin/analytics/payment-distribution` | `List<PaymentDistributionDto>` |

## 6.11 Admin Settings (`/api/admin/settings`)

Controller: `src/TBM.API/Controllers/V1/Admin/AdminSettingsController.cs`

All endpoints require role `Admin` or `SuperAdmin`.

| Method | Path | Request | Success Response |
|---|---|---|---|
| GET | `/api/admin/settings/payment` | none | `PaymentSettingsDto` |
| PUT | `/api/admin/settings/payment` | `PaymentSettingsDto` body | `"Payment settings updated"` |
| GET | `/api/admin/settings/ai` | none | `AISettingsDto` |
| PUT | `/api/admin/settings/ai` | `AISettingsDto` body | `"AI settings updated"` |
| GET | `/api/admin/settings/general` | none | `GeneralSettingsDto` |
| PUT | `/api/admin/settings/general` | `GeneralSettingsDto` body | `"General settings updated"` |

## 6.12 Paystack Webhook (`/api/webhooks/paystack`)

Controller: `src/TBM.API/Controllers/V1/Payments/PaystackWebhookController.cs`

| Method | Path | Auth | Request | Success Response | Failure Behavior |
|---|---|---|---|---|---|
| POST | `/api/webhooks/paystack` | Signature-based | raw webhook payload + header `x-paystack-signature` | 200 OK | 401 on invalid signature |

Webhook processing behavior:
- Verifies HMAC SHA512 using `Paystack:SecretKey`.
- Uses `data.reference` as idempotency key.
- On `charge.success`, loads order by order number/reference and updates:
  - `PaymentStatus = Paid`
  - `OrderStatus = Processing`
  - `PaidAt = UtcNow`

## 7. Request DTO Schemas

## 7.1 Auth DTOs

`RegisterDto`
- `email: string`
- `password: string`
- `confirmPassword: string`
- `firstName: string`
- `lastName: string`
- `phoneNumber?: string`

`LoginDto`
- `email: string`
- `password: string`

`AdminLoginDto`
- `email: string`
- `password: string`
- `rememberMe: bool`

`RefreshTokenDto`
- `refreshToken: string`

`ForgotPasswordDto`
- `email: string`

`ResetPasswordDto`
- `token: string`
- `newPassword: string`
- `confirmPassword: string`

## 7.2 Product and Category DTOs

`ProductFilterDto` (query)
- `pageNumber: int = 1`
- `pageSize: int = 12`
- `brandType?: int`
- `productType?: int`
- `categoryId?: Guid`
- `searchTerm?: string`
- `isFeatured?: bool`
- `activeOnly: bool = true`

`CreateProductDto`
- `name: string`
- `description: string`
- `shortDescription: string`
- `sku?: string`
- `brandType: int`
- `productType: int`
- `categoryId: Guid`
- `price?: decimal`
- `compareAtPrice?: decimal`
- `showPrice: bool = true`
- `stockQuantity?: int`
- `lowStockThreshold?: int`
- `trackInventory: bool = true`
- `isFeatured: bool`
- `displayOrder: int`
- `metaTitle?: string`
- `metaDescription?: string`
- `tags?: string`

`UpdateProductDto`
- `name: string`
- `description: string`
- `shortDescription: string`
- `sku?: string`
- `categoryId: Guid`
- `price?: decimal`
- `compareAtPrice?: decimal`
- `showPrice: bool`
- `stockQuantity?: int`
- `lowStockThreshold?: int`
- `trackInventory: bool`
- `isActive: bool`
- `isFeatured: bool`
- `displayOrder: int`
- `metaTitle?: string`
- `metaDescription?: string`
- `tags?: string`

`AddProductImageDto`
- `imageUrl: string`
- `altText?: string`
- `displayOrder: int`
- `isPrimary: bool`

`CreateCategoryDto`
- `name: string`
- `description: string`
- `brandType: int`
- `parentCategoryId?: Guid`
- `imageUrl?: string`
- `displayOrder: int`

`UpdateCategoryDto`
- `name: string`
- `description: string`
- `parentCategoryId?: Guid`
- `imageUrl?: string`
- `displayOrder: int`
- `isActive: bool`

## 7.3 Cart and Order DTOs

`AddToCartDto`
- `productId: Guid`
- `quantity: int = 1`

`UpdateCartItemDto`
- `quantity: int`

`CreateOrderDto`
- `shippingFullName: string`
- `shippingPhone: string`
- `shippingAddress: string`
- `shippingCity: string`
- `shippingState: string`
- `shippingNotes?: string`
- `customerNotes?: string`

`CancelOrderDto`
- `reason: string`

`OrderFilterDto` (query)
- `pageNumber: int = 1`
- `pageSize: int = 10`
- `userId?: Guid`
- `status?: int`
- `paymentStatus?: int`
- `fromDate?: DateTime`
- `toDate?: DateTime`
- `searchTerm?: string`

`UpdateOrderStatusDto`
- `status: int`
- `adminNotes?: string`
- `trackingNumber?: string`

`UpdatePaymentStatusDto`
- `paymentStatus: int`
- `paymentMethod?: int`
- `paymentReference?: string`

`TrackingDto`
- `trackingNumber: string`

## 7.4 AI DTOs

`CreateAIProjectDto`
- `sourceImageUrl: string`
- `generationType: AIGenerationType`
- `prompt?: string`
- `contextLabel?: string`

`GenerateImageDto`
- `projectId: Guid`
- `prompt: string`
- `sourceImageUrl: string`

`GenerateVideoDto`
- `projectId: Guid`
- `prompt: string`
- `sourceImageUrl?: string`
- `durationSeconds: int = 9`

## 7.5 Admin Settings DTOs

`PaymentSettingsDto`
- `basePlatformFee: decimal`
- `fixedFeePerTransaction: decimal`
- `defaultCurrency: string = "USD"`
- `gateways: List<PaymentGatewayDto>`

`PaymentGatewayDto`
- `id: string`
- `enabled: bool`
- `publicKey: string`
- `secretKey: string`

`AISettingsDto`
- `rateLimit: int`
- `timeoutSeconds: int`
- `models: List<AIModelDto>`

`AIModelDto`
- `id: string`
- `enabled: bool`
- `apiKey: string`
- `maxTokens: int`

`GeneralSettingsDto`
- `platformName: string`
- `supportEmail: string`
- `maintenanceMode: bool`
- `apiRateLimit: int`

## 8. Response DTO Schemas

## 8.1 Auth Response DTOs

`TokenResponseDto`
- `accessToken: string`
- `refreshToken: string`
- `expiresAt: DateTime`
- `user: UserInfoDto`

`UserInfoDto`
- `id: Guid`
- `email: string`
- `firstName: string`
- `lastName: string`
- `fullName: string`
- `roles: List<string>`

## 8.2 Product and Category Response DTOs

`ProductDto`
- `id: Guid`
- `name: string`
- `description: string`
- `shortDescription: string`
- `slug: string`
- `sku?: string`
- `brandType: int`
- `brandName: string`
- `productType: int`
- `productTypeName: string`
- `categoryId: Guid`
- `categoryName: string`
- `price?: decimal`
- `compareAtPrice?: decimal`
- `showPrice: bool`
- `priceDisplay: string`
- `stockQuantity?: int`
- `inStock: bool`
- `trackInventory: bool`
- `isActive: bool`
- `isFeatured: bool`
- `tags?: string`
- `images: List<ProductImageDto>`
- `primaryImageUrl?: string`
- `createdAt: DateTime`
- `updatedAt: DateTime`

`ProductImageDto`
- `id: Guid`
- `productId: Guid`
- `imageUrl: string`
- `altText?: string`
- `displayOrder: int`
- `isPrimary: bool`

`CategoryDto`
- `id: Guid`
- `name: string`
- `description: string`
- `slug: string`
- `brandType: int`
- `brandName: string`
- `parentCategoryId?: Guid`
- `parentCategoryName?: string`
- `imageUrl?: string`
- `displayOrder: int`
- `isActive: bool`
- `subCategories: List<CategoryDto>`
- `productCount: int`

## 8.3 Cart and Order Response DTOs

`CartDto`
- `id: Guid`
- `userId: Guid`
- `items: List<CartItemDto>`
- `totalItems: int`
- `subTotal: decimal`
- `createdAt: DateTime`
- `updatedAt: DateTime`

`CartItemDto`
- `id: Guid`
- `productId: Guid`
- `productName: string`
- `productSKU?: string`
- `productImageUrl?: string`
- `quantity: int`
- `unitPrice: decimal`
- `subTotal: decimal`
- `inStock: bool`
- `stockQuantity?: int`
- `addedAt: DateTime`

`OrderDto`
- `id: Guid`
- `orderNumber: string`
- `userId: Guid`
- `userEmail: string`
- `userFullName: string`
- `status: int`
- `statusName: string`
- `paymentStatus: int`
- `paymentStatusName: string`
- `paymentMethod?: int`
- `paymentMethodName?: string`
- `subTotal: decimal`
- `shippingCost: decimal`
- `tax: decimal`
- `discount: decimal`
- `total: decimal`
- `shippingFullName: string`
- `shippingPhone: string`
- `shippingAddress: string`
- `shippingCity: string`
- `shippingState: string`
- `shippingNotes?: string`
- `paymentReference?: string`
- `paidAt?: DateTime`
- `trackingNumber?: string`
- `shippedAt?: DateTime`
- `deliveredAt?: DateTime`
- `cancelledAt?: DateTime`
- `cancellationReason?: string`
- `customerNotes?: string`
- `adminNotes?: string`
- `items: List<OrderItemDto>`
- `createdAt: DateTime`
- `updatedAt: DateTime`

`OrderItemDto`
- `id: Guid`
- `productId: Guid`
- `productName: string`
- `productSKU?: string`
- `productImageUrl?: string`
- `quantity: int`
- `unitPrice: decimal`
- `subTotal: decimal`

## 8.4 Admin Response DTOs

`AdminUserListDto`
- `id: Guid`
- `email: string`
- `fullName: string`
- `status: string`
- `roles: List<string>`
- `createdAt: DateTime`

`AdminUserDetailsDto`
- `id: Guid`
- `email: string`
- `firstName: string`
- `lastName: string`
- `status: string`
- `roles: List<string>`
- `createdAt: DateTime`

`AdminOrderListDto`
- `id: Guid`
- `orderNumber: string`
- `customerName: string`
- `total: decimal`
- `status: string`
- `createdAt: DateTime`

`AdminOrderDetailsDto`
- `id: Guid`
- `orderNumber: string`
- `status: string`
- `total: decimal`
- `items: List<OrderItemDto>`
- `history: List<OrderStatusHistoryDto>`

`OrderStatusHistoryDto`
- `oldStatus: string`
- `newStatus: string`
- `changedAt: DateTime`
- `note?: string`

`AdminAnalyticsOverviewDto`
- `totalRevenue: decimal`
- `revenueGrowthPercentage: decimal`
- `totalOrders: int`
- `ordersGrowthPercentage: decimal`
- `totalUsers: int`
- `usersGrowthPercentage: decimal`
- `averageOrderValue: decimal`

`MonthlyRevenueDto`
- `year: int`
- `month: int`
- `revenue: decimal`

`PaymentDistributionDto`
- `paymentMethod: string`
- `revenue: decimal`

## 8.5 AI Raw Entity Responses

These entities are returned directly by AI endpoints.

`AIProject` (extends `AuditableEntity`)
- Base fields from `BaseEntity/AuditableEntity`:
  - `id: Guid`
  - `createdAt: DateTime`
  - `updatedAt?: DateTime`
  - `createdBy?: string`
  - `updatedBy?: string`
  - `deletedAt?: DateTime`
  - `deletedBy?: string`
  - `isDeleted: bool`
- Entity fields:
  - `userId: Guid`
  - `sourceImageUrl: string`
  - `generationType: AIGenerationType`
  - `status: AIProjectStatus`
  - `prompt?: string`
  - `negativePrompt?: string`
  - `contextLabel?: string`
  - `designs: ICollection<AIDesign>`

`AIDesign` (extends `AuditableEntity`)
- Base fields from `BaseEntity/AuditableEntity`
- Entity fields:
  - `aiProjectId: Guid`
  - `outputUrl: string`
  - `outputType: AIOutputType`
  - `width?: int`
  - `height?: int`
  - `durationSeconds?: double`
  - `provider?: string`
  - `providerJobId?: string`

## 9. Example Requests

## 9.1 Register

```http
POST /api/v1/auth/register
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "Pass123!",
  "confirmPassword": "Pass123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+2348000000000"
}
```

## 9.2 Login

```http
POST /api/v1/auth/login
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "Pass123!"
}
```

## 9.3 Create Product (SuperAdmin)

```http
POST /api/v1/products
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "name": "Sample Product",
  "description": "Long description",
  "shortDescription": "Short description",
  "sku": "SKU-001",
  "brandType": 1,
  "productType": 1,
  "categoryId": "00000000-0000-0000-0000-000000000000",
  "price": 12000,
  "compareAtPrice": 15000,
  "showPrice": true,
  "stockQuantity": 10,
  "lowStockThreshold": 2,
  "trackInventory": true,
  "isFeatured": false,
  "displayOrder": 1,
  "metaTitle": "Meta title",
  "metaDescription": "Meta description",
  "tags": "tag1,tag2"
}
```

## 9.4 Upload AI Room Image

```http
POST /api/v1/ai/upload-room
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

Form field:
- `file`: uploaded image file

## 10. Known API Design Notes

- Mixed response styles exist (enveloped and non-enveloped).
- Some admin endpoints return plain strings.
- Admin auth controller returns HTTP 200 even for logical failures (check `success` in payload).
- AI endpoints are exception-driven and may produce raw 500 responses with `{ error }`.
- `verify-email` is implemented as `POST` with query token instead of body payload.

