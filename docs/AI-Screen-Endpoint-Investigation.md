# AI Screen Endpoint Investigation

## Status note

This document captures the pre-refactor investigation state.

For the current recommended and partially implemented AI studio contract, use:

- `docs/AI-Target-Flow-Recommendation.md`
- `docs/AI-Backend-Refactor-Checklist.md`

## Purpose

This document maps each AI screen to the backend endpoints it should use, explains the request and response shapes, and calls out the current frontend/backend mismatches.

Use this as the reference for the frontend team when wiring the AI screens, and as the comparison baseline when we later fix backend alignment gaps.

## Important context

- Frontend requests currently go through the Next.js proxy as `/api/proxy/v1/...`.
- The real backend routes are `/api/v1/...`.
- All routes below require an authenticated user.

## Executive summary

### 1. Prompt-only text-to-image is not aligned today

If the design only gives the user a prompt field, there is no fully aligned backend flow for that screen yet.

Why:

- `POST /api/v1/ai/generate/image` is not a prompt-only generator.
- It only works for AI projects whose `generationType` is `ImageToImage`.
- It also expects an existing `projectId` and a `sourceImageUrl`.
- The current design-session flow also does not support prompt-only generation, because it requires:
  - room dimensions
  - an uploaded room image before generation

### 2. Image-to-image should use the design-session flow

For the current backend, the correct flow for "upload a room image and generate a design" is:

1. `POST /api/v1/designs/sessions`
2. `POST /api/v1/designs/sessions/{sessionId}/upload`
3. `POST /api/v1/designs/sessions/{sessionId}/generate`
4. `GET /api/v1/designs/sessions/{sessionId}/status`
5. Optional: `GET /api/v1/designs/sessions/{sessionId}`
6. Optional: `POST /api/v1/designs/sessions/{sessionId}/add-to-cart`

### 3. Image-to-video should use the AI project flow

For the current backend, the correct flow for "upload an image and animate it into a video" is:

1. `POST /api/v1/ai/upload-room`
2. `POST /api/v1/ai/projects`
3. `POST /api/v1/ai/generate/video`
4. Optional: `GET /api/v1/ai/projects`

## Screen-by-screen endpoint guide

## A. Text To Image Screen

### Design intent

User enters only a prompt, then expects the system to generate a new image.

### Current backend reality

This is **not fully supported by the current backend contract**.

### Do not use

`POST /api/v1/ai/generate/image`

Reason:

- It requires `projectId`.
- It requires an AI project whose `generationType` is `ImageToImage` (`1`).
- It expects a source image path either in the request body or already stored on the project.
- In service logic, it throws if the project is not `ImageToImage`.

Representative request shape:

```json
{
  "projectId": "00000000-0000-0000-0000-000000000000",
  "prompt": "Modern warm living room with oak and beige accents",
  "sourceImageUrl": "https://example.com/reference-room.png"
}
```

### Also not aligned

The current design-session route is also not prompt-only:

- `POST /api/v1/designs/sessions` requires `roomDimensions`
- `POST /api/v1/designs/sessions/{sessionId}/generate` will fail unless a photo has already been uploaded

### Recommendation for frontend team

For now, treat prompt-only text-to-image as a **backend gap**, not a frontend wiring issue.

If the product wants to keep the screen as prompt-only, the backend must be changed to support it. The current contracts do not match that design.

## B. Image To Image Screen

This is the closest fit to the current design-session backend.

### Step 1. Create a design session

**Backend route**

`POST /api/v1/designs/sessions`

**Frontend proxy route**

`POST /api/proxy/v1/designs/sessions`

**Use case**

Creates a design-generation session and stores the room metadata before image upload and generation.

**Required payload**

```json
{
  "projectName": "Modern kitchen redesign",
  "roomType": "Kitchen",
  "visionText": "Modern minimalist kitchen with marble countertops and warm wood accents",
  "tier": 2,
  "roomDimensions": {
    "length": 4.2,
    "width": 3.6,
    "height": 2.9
  }
}
```

### Important notes

- `projectName`, `roomType`, and `visionText` are required.
- `roomDimensions.length`, `roomDimensions.width`, and `roomDimensions.height` must all be greater than zero.
- `tier` values:
  - `1` = `Luxury`
  - `2` = `Economic`

**Success response**

```json
{
  "sessionId": "11111111-1111-1111-1111-111111111111",
  "sessionNumber": "DS-000001",
  "status": "Draft",
  "uploadUrl": "/api/v1/designs/sessions/11111111-1111-1111-1111-111111111111/upload"
}
```

**Failure response**

```json
{
  "success": false,
  "message": "Room dimensions must be greater than zero",
  "errors": []
}
```

### Step 2. Upload the reference image

**Backend route**

`POST /api/v1/designs/sessions/{sessionId}/upload`

**Frontend proxy route**

`POST /api/proxy/v1/designs/sessions/{sessionId}/upload`

**Use case**

Uploads the source room image that the design-session generator requires.

**Request**

- Content type: `multipart/form-data`
- Field name: `image`

Example form field:

```text
image=<binary file>
```

**Rules**

- File is required
- Must be an image
- Max size is 10 MB

**Success response**

```json
{
  "originalImageUrl": "https://cdn.example.com/design-session/source-room.png",
  "status": "PhotoUploaded"
}
```

**Failure response**

```json
{
  "success": false,
  "message": "Only image files are allowed",
  "errors": []
}
```

### Step 3. Request generation

**Backend route**

`POST /api/v1/designs/sessions/{sessionId}/generate`

**Frontend proxy route**

`POST /api/proxy/v1/designs/sessions/{sessionId}/generate`

**Use case**

Queues the design session for processing after the source room image is already uploaded.

**Required payload**

```json
{
  "generateBOM": true
}
```

### Important notes

- The current controller expects a request body.
- The current controller rejects `generateBOM: false`.
- The current service rejects generation when no photo has been uploaded.

**Success response**

```json
{
  "status": "Processing",
  "estimatedTime": 45,
  "statusUrl": "/api/v1/designs/sessions/11111111-1111-1111-1111-111111111111/status"
}
```

**Failure response**

```json
{
  "success": false,
  "message": "Upload a photo before generating the design",
  "errors": []
}
```

### Step 4. Poll for status

**Backend route**

`GET /api/v1/designs/sessions/{sessionId}/status`

**Frontend proxy route**

`GET /api/proxy/v1/designs/sessions/{sessionId}/status`

**Use case**

Poll until the session reaches a terminal state.

Terminal states:

- `Generated`
- `Failed`
- `Ordered`

Status enum values:

- `1` = `Draft`
- `2` = `PhotoUploaded`
- `3` = `Processing`
- `4` = `Generated`
- `5` = `CartCreated`
- `6` = `Ordered`
- `7` = `ConvertedToProject`
- `8` = `Failed`

**Success response**

```json
{
  "status": "Generated",
  "progress": 100,
  "currentStep": "Completed",
  "imageUrl": "https://cdn.example.com/design-session/generated-image.png",
  "bomGenerated": true,
  "errorMessage": null
}
```

### Step 5. Optional detail fetch

**Backend route**

`GET /api/v1/designs/sessions/{sessionId}`

**Use case**

Fetches the full session record and generated bill of materials.

**Success response shape**

```json
{
  "session": {
    "sessionId": "11111111-1111-1111-1111-111111111111",
    "sessionNumber": "DS-000001",
    "projectName": "Modern kitchen redesign",
    "roomType": "Kitchen",
    "visionText": "Modern minimalist kitchen with marble countertops and warm wood accents",
    "tier": 2,
    "status": "Generated",
    "progress": 100,
    "currentStep": "Completed",
    "errorMessage": null,
    "originalImageUrl": "https://cdn.example.com/design-session/source-room.png",
    "generatedImageUrl": "https://cdn.example.com/design-session/generated-image.png",
    "roomLength": 4.2,
    "roomWidth": 3.6,
    "roomHeight": 2.9,
    "bomId": "22222222-2222-2222-2222-222222222222",
    "orderId": null,
    "projectId": null,
    "createdAt": "2026-04-06T10:00:00Z",
    "updatedAt": "2026-04-06T10:02:00Z"
  },
  "billOfMaterials": {
    "id": "22222222-2222-2222-2222-222222222222",
    "designSessionId": "11111111-1111-1111-1111-111111111111",
    "bomNumber": "BOM-000001",
    "totalEstimatedCost": 2500.00,
    "itemCount": 8,
    "status": "Generated",
    "items": []
  },
  "allItemsInStock": true
}
```

### Step 6. Optional add to cart

**Backend route**

`POST /api/v1/designs/sessions/{sessionId}/add-to-cart`

**Required payload**

```json
{
  "addAll": true,
  "itemIds": []
}
```

**Success response**

```json
{
  "cartId": "33333333-3333-3333-3333-333333333333",
  "itemsAdded": 8,
  "totalAmount": 2500.00
}
```

## C. Image To Video Screen

This flow is based on the AI project endpoints, not the design-session endpoints.

### Step 1. Upload the source image

**Backend route**

`POST /api/v1/ai/upload-room`

**Frontend proxy route**

`POST /api/proxy/v1/ai/upload-room`

**Use case**

Uploads the image that will later be attached to the AI project and animated into video.

**Request**

- Content type: `multipart/form-data`
- Field name: `file`

Example form field:

```text
file=<binary file>
```

**Success response**

```json
{
  "success": true,
  "imageUrl": "https://cdn.example.com/ai/source-room.png"
}
```

### Step 2. Create the AI project

**Backend route**

`POST /api/v1/ai/projects`

**Frontend proxy route**

`POST /api/proxy/v1/ai/projects`

**Use case**

Creates the AI project record that generation endpoints depend on.

**Required payload for image-to-video**

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "generationType": 2,
  "prompt": "Create a slow cinematic fly-through of this living room",
  "contextLabel": "Living Room"
}
```

`generationType` values:

- `1` = `ImageToImage`
- `2` = `ImageToVideo`

### Important notes

- This endpoint does **not** accept the current frontend payload of `{ name, description }`.
- `sourceImageUrl` is part of the current backend contract.
- The controller returns the raw `AIProject` entity, not a slim DTO.

**Representative success response**

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "createdAt": "2026-04-06T10:00:00Z",
  "updatedAt": null,
  "createdBy": null,
  "updatedBy": null,
  "deletedAt": null,
  "deletedBy": null,
  "isDeleted": false,
  "userId": "55555555-5555-5555-5555-555555555555",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "generationType": 2,
  "status": 1,
  "prompt": "Create a slow cinematic fly-through of this living room",
  "negativePrompt": null,
  "contextLabel": "Living Room",
  "designs": []
}
```

### Step 3. Generate the video

**Backend route**

`POST /api/v1/ai/generate/video`

**Frontend proxy route**

`POST /api/proxy/v1/ai/generate/video`

**Use case**

Runs the actual video generation for an already-created `ImageToVideo` project.

**Required payload**

```json
{
  "projectId": "44444444-4444-4444-4444-444444444444",
  "prompt": "Create a slow cinematic fly-through of this living room",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "durationSeconds": 9
}
```

### Important notes

- `projectId` must belong to the authenticated user.
- The project must have `generationType = 2`.
- The current backend uses `dto.Prompt` directly during generation.
- The current backend does **not** fall back to the prompt stored on the project.
- So sending only `{ projectId }` is not enough.

**Representative success response**

```json
{
  "id": "66666666-6666-6666-6666-666666666666",
  "createdAt": "2026-04-06T10:01:00Z",
  "updatedAt": null,
  "createdBy": null,
  "updatedBy": null,
  "deletedAt": null,
  "deletedBy": null,
  "isDeleted": false,
  "aiProjectId": "44444444-4444-4444-4444-444444444444",
  "outputUrl": "https://res.cloudinary.com/.../generated-video.mp4",
  "outputType": 2,
  "width": 1280,
  "height": 720,
  "durationSeconds": 9,
  "provider": "OpenAI",
  "providerJobId": "provider-job-id",
  "isPublic": false,
  "publishedAt": null
}
```

### Step 4. Optional project list refresh

**Backend route**

`GET /api/v1/ai/projects`

**Use case**

Returns the user's projects with the latest output URL and design count.

**Success response shape**

```json
[
  {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": 3,
    "generationType": 2,
    "contextLabel": "Living Room",
    "createdAt": "2026-04-06T10:00:00Z",
    "latestDesignUrl": "https://res.cloudinary.com/.../generated-video.mp4",
    "designCount": 1
  }
]
```

## Error response behavior

The backend is not fully uniform in how it returns errors across AI routes.

### Design-session error shape

Design-session failures are usually returned like this:

```json
{
  "success": false,
  "message": "Upload a photo before generating the design",
  "errors": []
}
```

### AI project and generation error shapes

AI controller failures usually return one of these:

```json
{
  "success": false,
  "error": "Project generation type does not support video generation."
}
```

or quota failures:

```json
{
  "success": false,
  "error": {
    "code": "subscription_quota_exceeded",
    "message": "AI generation quota exceeded."
  }
}
```

Frontend consumers should not assume one single error shape for all AI routes.

## Current frontend/backend mismatches

These are the gaps that matter immediately for the AI screens.

### 1. Text-to-image screen is prompt-only, but backend is not

Current design/backend mismatch:

- Screen input exposes only a prompt
- Backend requires either:
  - source image based AI project flow, or
  - design session with room dimensions plus uploaded photo

### 2. Frontend design-session creation currently omits room dimensions

Current frontend call shape:

```json
{
  "projectName": "...",
  "roomType": "...",
  "visionText": "...",
  "tier": 2
}
```

Backend actually requires:

```json
{
  "projectName": "...",
  "roomType": "...",
  "visionText": "...",
  "tier": 2,
  "roomDimensions": {
    "length": 1,
    "width": 1,
    "height": 1
  }
}
```

### 3. Frontend design-session generation currently sends no body

Current frontend helper posts without a JSON body.

Backend expects:

```json
{
  "generateBOM": true
}
```

### 4. Text-to-image currently tries to generate without an uploaded photo

In the current backend:

- image upload is optional in the frontend for text-to-image
- image upload is mandatory in the backend before design-session generation

### 5. Frontend image-to-video project creation payload is wrong

Current frontend sends:

```json
{
  "name": "Prompt title",
  "description": "Full prompt"
}
```

Backend expects something like:

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "generationType": 2,
  "prompt": "Full prompt",
  "contextLabel": "Living Room"
}
```

### 6. Frontend image-to-video generation payload is incomplete

Current frontend sends:

```json
{
  "projectId": "44444444-4444-4444-4444-444444444444"
}
```

Backend actually expects:

```json
{
  "projectId": "44444444-4444-4444-4444-444444444444",
  "prompt": "Create a slow cinematic fly-through of this living room",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "durationSeconds": 9
}
```

### 7. Frontend AI endpoint comments are misleading

The current frontend comments imply:

- `generateImage` is prompt-first
- `transformImage` can accept `{ imageUrl, prompt, projectId? }`
- `generateVideo` only needs `{ projectId, settings? }`

Those comments do not match the current backend contracts.

## What the frontend team should use right now

### For image-to-image

Use:

1. `POST /api/v1/designs/sessions`
2. `POST /api/v1/designs/sessions/{sessionId}/upload`
3. `POST /api/v1/designs/sessions/{sessionId}/generate`
4. `GET /api/v1/designs/sessions/{sessionId}/status`

But only after the frontend sends:

- room dimensions on create
- uploaded reference photo
- `{ "generateBOM": true }` on generate

### For image-to-video

Use:

1. `POST /api/v1/ai/upload-room`
2. `POST /api/v1/ai/projects`
3. `POST /api/v1/ai/generate/video`

And make sure:

- upload uses form field `file`
- project creation sends `sourceImageUrl`, `generationType`, `prompt`, `contextLabel`
- video generation sends `projectId`, `prompt`, and preferably `sourceImageUrl`

### For prompt-only text-to-image

Do **not** treat this as a solved integration yet.

Current status:

- the design suggests prompt-only
- the backend does not currently expose a clean prompt-only generation contract

This needs a backend alignment decision.

## Recommended backend alignment decision

If the product direction is to keep the AI design screen prompt-only, then the backend should be updated so the frontend can call a single prompt-based image generation flow without requiring:

- `sourceImageUrl`
- pre-created image-to-image project state
- room dimensions
- photo upload

Until that change is made, the frontend can only be fully correct for:

- image-to-image with an uploaded photo
- image-to-video with an uploaded photo
