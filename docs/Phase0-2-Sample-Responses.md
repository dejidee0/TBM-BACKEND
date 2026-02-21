# Phase 0-2 Sample Response Payloads

Generated from current controllers on February 21, 2026.

## 1) Test Setup

- Base URL example: `https://localhost:5001`
- Protected routes require:
  - `Authorization: Bearer <jwt_token>`
- Common auth failure:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401
}
```

## 2) Phase 0 Samples

### POST `/Auth/register` (also works on `/api/Auth/register`, `/api/v1/Auth/register`)

```json
{
  "success": true,
  "message": "Registration successful. Please verify your email.",
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "d5f4ff9f4b034980a3a3...",
    "expiresAt": "2026-02-21T14:30:00Z",
    "user": {
      "id": "2f9d7b7d-1a89-49f1-95ea-1d87dbcb7d90",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "fullName": "John Doe",
      "roles": [
        "Customer"
      ]
    }
  },
  "errors": null
}
```

### POST `/Auth/login`

```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "a5b9f0af2d6d4a5b8f14...",
    "expiresAt": "2026-02-21T14:35:00Z",
    "user": {
      "id": "2f9d7b7d-1a89-49f1-95ea-1d87dbcb7d90",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "fullName": "John Doe",
      "roles": [
        "Customer"
      ]
    }
  },
  "errors": null
}
```

### GET `/Auth/me` (also `/api/auth/me`, `/api/v1/auth/me`)

```json
{
  "userId": "2f9d7b7d-1a89-49f1-95ea-1d87dbcb7d90",
  "email": "user@example.com",
  "name": "John Doe",
  "roles": [
    "Customer"
  ]
}
```

### POST `/Auth/forgot-password`

```json
{
  "success": true,
  "message": "If the email exists, a password reset link has been sent.",
  "data": true,
  "errors": null
}
```

### POST `/Auth/verify-email` (body flow)

```json
{
  "success": true,
  "message": "Email verified successfully",
  "data": true,
  "errors": null
}
```

### POST `/Auth/verify-email?token=...` (token flow)

```json
{
  "success": true,
  "message": "Email verified successfully",
  "data": true,
  "errors": null
}
```

### POST `/Auth/resend-verification`

```json
{
  "success": true,
  "message": "Verification email sent successfully",
  "data": true,
  "errors": null
}
```

### GET `/auth/google` and GET `/auth/apple` (currently disabled mode)

```json
{
  "success": false,
  "message": "Google OAuth is not configured yet.",
  "data": null,
  "errors": [
    "Use POST /api/v1/auth/login with email/password.",
    "If OAuth is required, configure provider credentials and callback settings."
  ]
}
```

## 3) Phase 1 Samples

### GET `/api/flooring`

```json
{
  "products": [
    {
      "id": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "name": "Premium Oak Tile",
      "slug": "premium-oak-tile",
      "price": 24500.0,
      "primaryImageUrl": "https://cdn.example.com/p/oak.jpg",
      "inStock": true
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 12,
    "total": 1,
    "totalPages": 1,
    "hasMore": false
  },
  "filters": {
    "category": "tiles",
    "materialType": "wood",
    "minPrice": 10000.0,
    "maxPrice": 50000.0,
    "sort": "price_asc"
  }
}
```

### GET `/materials`

```json
{
  "materials": [
    {
      "id": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "name": "Premium Oak Tile",
      "slug": "premium-oak-tile",
      "description": "Engineered oak wood finish",
      "price": 24500.0,
      "image": "https://cdn.example.com/p/oak.jpg",
      "category": "Tiles",
      "inStock": true
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 12,
    "total": 1,
    "totalPages": 1,
    "hasMore": false
  },
  "filters": {
    "category": null,
    "materialType": null,
    "minPrice": null,
    "maxPrice": null,
    "sort": null
  }
}
```

### GET `/materials/{id}`

```json
{
  "material": {
    "id": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
    "name": "Premium Oak Tile",
    "slug": "premium-oak-tile",
    "description": "Engineered oak wood finish",
    "price": 24500.0,
    "image": "https://cdn.example.com/p/oak.jpg",
    "category": "Tiles",
    "inStock": true
  }
}
```

### GET `/api/cart`

```json
{
  "items": [
    {
      "id": "717e8725-c639-4172-af8d-5f08c4c4cc76",
      "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "productName": "Premium Oak Tile",
      "productSKU": "TILE-OAK-001",
      "productImageUrl": "https://cdn.example.com/p/oak.jpg",
      "quantity": 2,
      "unitPrice": 24500.0,
      "subTotal": 49000.0,
      "inStock": true,
      "stockQuantity": 35,
      "addedAt": "2026-02-21T11:00:00Z"
    }
  ],
  "subtotal": 49000.0,
  "shipping": 5000.0,
  "taxRate": 0.075,
  "estimatedDelivery": "2026-02-24"
}
```

### POST `/api/cart/add`

```json
{
  "success": true,
  "item": {
    "id": "717e8725-c639-4172-af8d-5f08c4c4cc76",
    "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
    "productName": "Premium Oak Tile",
    "quantity": 2,
    "unitPrice": 24500.0,
    "subTotal": 49000.0
  },
  "message": "Item added to cart successfully"
}
```

### PUT `/api/cart/items/{itemId}`

```json
{
  "success": true,
  "item": {
    "id": "717e8725-c639-4172-af8d-5f08c4c4cc76",
    "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
    "productName": "Premium Oak Tile",
    "quantity": 3,
    "unitPrice": 24500.0,
    "subTotal": 73500.0
  }
}
```

### DELETE `/api/cart/items/{itemId}`

```json
{
  "success": true
}
```

### POST `/api/cart/apply-promo`

```json
{
  "success": true,
  "code": "WELCOME10",
  "discount": 10.0,
  "type": "percentage",
  "discountAmount": 4900.0
}
```

### GET `/api/cart/related`

```json
[
  {
    "id": "f58df035-b26c-4f3f-855f-661d2d3a5138",
    "name": "Walnut Plank",
    "price": 30000.0,
    "image": "https://cdn.example.com/p/walnut.jpg",
    "rating": 4.5
  }
]
```

### GET `/api/checkout`

```json
{
  "items": [
    {
      "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "name": "Premium Oak Tile",
      "unitPrice": 24500.0,
      "quantity": 2,
      "subtotal": 49000.0,
      "image": "https://cdn.example.com/p/oak.jpg"
    }
  ],
  "subtotal": 49000.0,
  "shipping": 5000.0,
  "tax": 3675.0,
  "discount": 0.0,
  "total": 57675.0,
  "savedAddresses": [
    {
      "id": "165f9f3e-8c85-4864-af22-a1e86e3984e0",
      "fullName": "John Doe",
      "street": "12 Allen Ave",
      "city": "Ikeja",
      "state": "Lagos",
      "postalCode": "100001",
      "country": "Nigeria",
      "phone": "08012345678",
      "deliveryNotes": null,
      "isDefault": true
    }
  ],
  "defaultAddress": {
    "id": "165f9f3e-8c85-4864-af22-a1e86e3984e0",
    "fullName": "John Doe",
    "street": "12 Allen Ave",
    "city": "Ikeja",
    "state": "Lagos",
    "postalCode": "100001",
    "country": "Nigeria",
    "phone": "08012345678",
    "deliveryNotes": null,
    "isDefault": true
  }
}
```

### POST `/api/checkout/validate-promo`

```json
{
  "success": true,
  "code": "WELCOME10",
  "discount": 10.0,
  "type": "percentage",
  "discountAmount": 4900.0,
  "message": "Promo code applied successfully."
}
```

### POST `/api/checkout/payment`

```json
{
  "success": true,
  "orderId": "3c7f6a6c-91d5-4015-af9f-1aeb6098f016",
  "orderNumber": "ORD202602210001",
  "message": "Checkout payment request accepted.",
  "idempotent": false
}
```

### GET `/api/orders`

```json
[
  {
    "id": "3c7f6a6c-91d5-4015-af9f-1aeb6098f016",
    "orderNumber": "ORD202602210001",
    "status": "Pending",
    "paymentStatus": "Pending",
    "total": 57675.0,
    "createdAt": "2026-02-21T12:00:00Z"
  }
]
```

### GET `/api/orders/{orderId}`

```json
{
  "id": "3c7f6a6c-91d5-4015-af9f-1aeb6098f016",
  "orderNumber": "ORD202602210001",
  "userId": "2f9d7b7d-1a89-49f1-95ea-1d87dbcb7d90",
  "userEmail": "user@example.com",
  "status": 0,
  "statusName": "Pending",
  "paymentStatus": 0,
  "paymentStatusName": "Pending",
  "subTotal": 49000.0,
  "shippingCost": 5000.0,
  "tax": 3675.0,
  "discount": 0.0,
  "total": 57675.0,
  "shippingFullName": "John Doe",
  "shippingPhone": "08012345678",
  "shippingAddress": "12 Allen Ave",
  "shippingCity": "Ikeja",
  "shippingState": "Lagos",
  "items": [
    {
      "id": "0101df6e-2ce0-48d9-93f2-5721d7b46f56",
      "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "productName": "Premium Oak Tile",
      "quantity": 2,
      "unitPrice": 24500.0,
      "subTotal": 49000.0
    }
  ],
  "createdAt": "2026-02-21T12:00:00Z",
  "updatedAt": "2026-02-21T12:00:00Z"
}
```

### GET `/api/orders/{orderId}/invoice`

```json
{
  "success": true,
  "url": "https://localhost:5001/api/v1/orders/3c7f6a6c-91d5-4015-af9f-1aeb6098f016/invoice/document"
}
```

### GET `/api/v1/orders/{orderId}/invoice/document`

```json
{
  "invoiceNumber": "INV-ORD202602210001",
  "orderNumber": "ORD202602210001",
  "issuedAt": "2026-02-21T12:05:00Z",
  "customer": {
    "name": "John Doe",
    "email": "user@example.com",
    "phone": "08012345678"
  },
  "shippingAddress": {
    "fullName": "John Doe",
    "address": "12 Allen Ave",
    "city": "Ikeja",
    "state": "Lagos"
  },
  "items": [
    {
      "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "name": "Premium Oak Tile",
      "sku": "TILE-OAK-001",
      "quantity": 2,
      "unitPrice": 24500.0,
      "subTotal": 49000.0
    }
  ],
  "totals": {
    "subTotal": 49000.0,
    "shipping": 5000.0,
    "tax": 3675.0,
    "discount": 0.0,
    "total": 57675.0
  },
  "payment": {
    "status": "Pending",
    "method": null,
    "reference": "idem-3f7f2e3e",
    "paidAt": null
  }
}
```

## 4) Phase 2 Samples

### GET `/api/saved`

```json
{
  "items": [
    {
      "id": "bb1f96dc-8f35-4ca1-8569-a360444b02e7",
      "productId": "3c030f31-0f79-4d6e-8382-bad86d4df5c8",
      "name": "Premium Oak Tile",
      "category": "Tiles",
      "price": 24500.0,
      "image": "https://cdn.example.com/p/oak.jpg",
      "savedAt": "2026-02-21T12:12:00Z"
    }
  ],
  "boards": [
    {
      "id": "cce6c3e4-7d64-48e2-af18-5f8be795bbca",
      "name": "Kitchen Ideas",
      "itemCount": 1,
      "createdAt": "2026-02-21T12:20:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 10,
    "total": 1,
    "totalPages": 1,
    "hasMore": false
  }
}
```

### POST `/api/saved`

```json
{
  "success": true
}
```

### DELETE `/api/saved/{id}`

```json
{
  "success": true
}
```

### POST `/api/saved/{id}/add-to-cart`

```json
{
  "success": true,
  "message": "Item added to cart"
}
```

### POST `/api/saved/{id}/add-to-moodboard`

```json
{
  "success": true,
  "message": "Item added to moodboard"
}
```

### POST `/api/saved/create-board`

```json
{
  "success": true,
  "boardId": "cce6c3e4-7d64-48e2-af18-5f8be795bbca",
  "boardName": "Kitchen Ideas",
  "itemCount": 3
}
```

### POST `/api/saved/buy-all`

```json
{
  "success": true,
  "total": 73500.0,
  "itemCount": 3
}
```

### GET `/api/designs`

```json
{
  "designs": [
    {
      "id": "a6453472-3a61-4d55-89d9-95b57eb675dc",
      "projectId": "ac39cd86-a09c-45f8-a1a6-2fddc2681b66",
      "roomType": "kitchen",
      "prompt": "Modern oak and warm lighting",
      "url": "https://replicate.delivery/pbxt/.../output.png",
      "outputType": "Image",
      "createdAt": "2026-02-21T12:30:00Z",
      "isFavorite": true
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 10,
    "total": 1,
    "totalPages": 1,
    "hasMore": false
  }
}
```

### GET `/api/designs/{id}`

```json
{
  "id": "a6453472-3a61-4d55-89d9-95b57eb675dc",
  "projectId": "ac39cd86-a09c-45f8-a1a6-2fddc2681b66",
  "outputUrl": "https://replicate.delivery/pbxt/.../output.png",
  "outputType": "Image",
  "width": 1280,
  "height": 720,
  "durationSeconds": null,
  "prompt": "Modern oak and warm lighting",
  "roomType": "kitchen",
  "isFavorite": true,
  "createdAt": "2026-02-21T12:30:00Z"
}
```

### POST `/api/designs/{id}/favorite`

```json
{
  "success": true,
  "isFavorite": true
}
```

### DELETE `/api/designs/{id}`

```json
{
  "success": true
}
```

### GET `/api/designs/{id}/download?quality=high`

```json
{
  "success": true,
  "downloadUrl": "https://replicate.delivery/pbxt/.../output.png?quality=high"
}
```

### POST `/api/designs/{id}/share`

```json
{
  "success": true,
  "shareUrl": "https://localhost:5001/api/designs/a6453472-3a61-4d55-89d9-95b57eb675dc/download?token=7f7a53..."
}
```

### GET `/api/dashboard/recent-order`

```json
{
  "hasOrder": true,
  "orderId": "3c7f6a6c-91d5-4015-af9f-1aeb6098f016",
  "orderNumber": "ORD202602210001",
  "status": "Pending",
  "paymentStatus": "Pending",
  "total": 57675.0,
  "createdAt": "2026-02-21T12:00:00Z"
}
```

### GET `/api/dashboard/latest-design`

```json
{
  "hasDesign": true,
  "designId": "a6453472-3a61-4d55-89d9-95b57eb675dc",
  "outputUrl": "https://replicate.delivery/pbxt/.../output.png",
  "outputType": "Image",
  "createdAt": "2026-02-21T12:30:00Z"
}
```

### GET `/api/dashboard/consultations`

```json
{
  "upcomingCount": 0,
  "completedCount": 0,
  "nextConsultation": null
}
```

### GET `/api/dashboard/saved-items`

```json
{
  "totalSaved": 5,
  "latestSavedIds": [
    "bb1f96dc-8f35-4ca1-8569-a360444b02e7",
    "fc77af21-2af4-4fb3-ab9f-a4e985d5ef2d"
  ]
}
```

### GET `/api/dashboard/orders/{orderId}/tracking`

```json
{
  "success": true,
  "trackingUrl": "https://tracking.example.com/track/TBM1234567"
}
```

### GET `/api/account/profile`

```json
{
  "id": "2f9d7b7d-1a89-49f1-95ea-1d87dbcb7d90",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "fullName": "John Doe",
  "phoneNumber": "08012345678",
  "isActive": true,
  "emailVerified": true,
  "avatarUrl": "https://res.cloudinary.com/.../avatar.jpg",
  "addresses": [
    {
      "id": "165f9f3e-8c85-4864-af22-a1e86e3984e0",
      "fullName": "John Doe",
      "street": "12 Allen Ave",
      "city": "Ikeja",
      "state": "Lagos",
      "postalCode": "100001",
      "country": "Nigeria",
      "phone": "08012345678",
      "deliveryNotes": null,
      "isDefault": true
    }
  ]
}
```

### PUT `/api/account/profile`

```json
{
  "success": true,
  "data": {
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "08012345678"
  }
}
```

### PUT `/api/account/email`

```json
{
  "success": true,
  "message": "Email updated successfully"
}
```

### PUT `/api/account/phone`

```json
{
  "success": true,
  "message": "Phone updated successfully"
}
```

### POST `/api/account/addresses`

```json
{
  "success": true,
  "data": {
    "id": "165f9f3e-8c85-4864-af22-a1e86e3984e0",
    "fullName": "John Doe",
    "street": "12 Allen Ave",
    "city": "Ikeja",
    "state": "Lagos",
    "postalCode": "100001",
    "country": "Nigeria",
    "phone": "08012345678",
    "deliveryNotes": null,
    "isDefault": true
  }
}
```

### PUT `/api/account/addresses/{addressId}`

```json
{
  "success": true
}
```

### DELETE `/api/account/addresses/{addressId}`

```json
{
  "success": true
}
```

### PUT `/api/account/addresses/{addressId}/default`

```json
{
  "success": true
}
```

### PUT `/api/account/password`

```json
{
  "success": true,
  "message": "Password updated successfully"
}
```

### GET `/api/account/security`

```json
{
  "twoFactorEnabled": false
}
```

### PUT `/api/account/security/2fa`

```json
{
  "success": true
}
```

### GET `/api/account/notifications`

```json
{
  "emailOrderUpdates": true,
  "emailPromotions": false,
  "pushOrderUpdates": true,
  "pushMarketing": false
}
```

### PUT `/api/account/notifications`

```json
{
  "success": true
}
```

### GET `/api/account/brand-access`

```json
{
  "roles": [
    "Customer"
  ],
  "canUseStore": true,
  "canUseAdmin": false
}
```

### POST `/api/account/deactivate`

```json
{
  "success": true,
  "message": "Account deactivated"
}
```

### DELETE `/api/account`

```json
{
  "success": true,
  "message": "Account deleted successfully"
}
```

### POST `/api/account/avatar` (multipart/form-data)

```json
{
  "success": true,
  "avatarUrl": "https://res.cloudinary.com/.../avatar-2f9d...jpg"
}
```

## 5) Common Error Samples

### Invalid promo

```json
{
  "success": false,
  "message": "Invalid promo code."
}
```

### Missing account password on delete

```json
{
  "success": false,
  "message": "Password is required and must be valid"
}
```

### Ownership-protected design/saved/order

```json
{
  "success": false,
  "message": "Design not found"
}
```
