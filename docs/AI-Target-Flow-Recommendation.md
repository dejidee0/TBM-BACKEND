# AI Target Flow Recommendation

## Goal

This document captures the agreed product direction for the AI studio flow:

- user provides both:
  - a text prompt
  - a source image upload
- user chooses desired output:
  - `image`
  - `video`
- `video` is the primary and default output path
- the system should collect enough context to improve output accuracy

This is the target flow the backend and frontend should align around.

## Product decision

### Required inputs

For AI generation, the frontend should always collect:

- `prompt`
- `source image`
- `output type`

### Output selection

Supported outputs:

- `image`
- `video`

Default and priority:

- `video` should be the default selected output
- `video` is the primary use case the backend must support reliably first
- `image` remains supported as an alternate output mode

### Why this flow is better

Using both prompt and source image gives the model:

- visual grounding from the uploaded room/reference image
- creative direction from the written prompt
- better control over output accuracy and relevance

This is more aligned with the product than a prompt-only generation flow.

## Recommended user journey

## 1. User opens AI Studio

The screen should ask for:

- reference image upload
- prompt
- optional context fields such as room type
- output selector with:
  - `Video` preselected
  - `Image` as secondary

## 2. User uploads image

The frontend uploads the image first and receives a hosted `imageUrl`.

## 3. User submits prompt and chooses output type

The frontend creates an AI project that stores:

- uploaded image URL
- prompt
- selected generation type
- optional context label like `Living Room`, `Kitchen`, `Office`

## 4. Backend generates selected output

Based on the user's output selection:

- if output is `image`, generate an image
- if output is `video`, generate a video

## 5. Frontend shows result

The frontend displays:

- generated asset URL
- output type
- project status

## Recommended backend flow

For this product direction, the AI studio should use the AI project endpoints, not the design-session endpoints.

Why:

- design sessions are more tied to BOM, cart, and downstream design-order flow
- AI project endpoints are closer to pure media generation
- this requirement is about prompt + image driven media generation

## Endpoint sequence

### Step 1. Upload source image

`POST /api/v1/ai/upload-room`

Request:

- `multipart/form-data`
- field name: `file`

Success response:

```json
{
  "success": true,
  "imageUrl": "https://cdn.example.com/ai/source-room.png"
}
```

### Step 2. Create AI project

`POST /api/v1/ai/projects`

Recommended request for image output:

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "outputType": 1,
  "prompt": "Transform this room into a modern warm living room with oak wood and soft beige lighting",
  "contextLabel": "Living Room"
}
```

Recommended request for video output:

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "outputType": 2,
  "prompt": "Create a cinematic walkthrough of this room with warm lighting and modern furniture styling",
  "contextLabel": "Living Room"
}
```

Output type values:

- `1` = `Image`
- `2` = `Video`

Generation type values remain supported as a backward-compatible alias:

- `1` = `ImageToImage`
- `2` = `ImageToVideo`

### Step 3A. Generate image when output type is image

`POST /api/v1/ai/generate/image`

Minimum request:

```json
{
  "projectId": "11111111-1111-1111-1111-111111111111"
}
```

Optional override request:

```json
{
  "projectId": "11111111-1111-1111-1111-111111111111",
  "prompt": "Transform this room into a modern warm living room with oak wood and soft beige lighting",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png"
}
```

Representative success response:

```json
{
  "designId": "22222222-2222-2222-2222-222222222222",
  "projectId": "11111111-1111-1111-1111-111111111111",
  "outputUrl": "https://res.cloudinary.com/.../generated-image.png",
  "outputType": 1,
  "width": 1024,
  "height": 1024,
  "durationSeconds": null,
  "provider": "OpenAI",
  "providerJobId": "provider-job-id",
  "createdAt": "2026-04-06T10:00:00Z"
}
```

### Step 3B. Generate video when output type is video

`POST /api/v1/ai/generate/video`

Minimum request:

```json
{
  "projectId": "33333333-3333-3333-3333-333333333333",
  "durationSeconds": 9
}
```

Optional override request:

```json
{
  "projectId": "33333333-3333-3333-3333-333333333333",
  "prompt": "Create a cinematic walkthrough of this room with warm lighting and modern furniture styling",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "durationSeconds": 9
}
```

Representative success response:

```json
{
  "designId": "44444444-4444-4444-4444-444444444444",
  "projectId": "33333333-3333-3333-3333-333333333333",
  "outputUrl": "https://res.cloudinary.com/.../generated-video.mp4",
  "outputType": 2,
  "width": 1280,
  "height": 720,
  "durationSeconds": 9,
  "provider": "OpenAI",
  "providerJobId": "provider-job-id",
  "createdAt": "2026-04-06T10:01:00Z"
}
```

### Step 4. Refresh projects if needed

`GET /api/v1/ai/projects`

Representative response:

```json
[
  {
    "id": "33333333-3333-3333-3333-333333333333",
    "status": 3,
    "generationType": 2,
    "outputType": 2,
    "contextLabel": "Living Room",
    "createdAt": "2026-04-06T10:00:00Z",
    "latestDesignUrl": "https://res.cloudinary.com/.../generated-video.mp4",
    "designCount": 1
  }
]
```

## Frontend guidance

## What the frontend should send

For all AI studio submissions:

- always upload a source image first
- always send a prompt
- always send a selected output type

### UI recommendation

The frontend form should include:

- image uploader
- prompt textarea or input
- room type or context label
- output toggle:
  - `Video` default
  - `Image` secondary
- optional video duration selector if product wants it later

### Validation rules

The frontend should prevent submission when:

- no image uploaded
- prompt is empty
- no output type selected

## Backend contract recommendation

To make this flow explicit and consistent, the backend should treat the following fields as mandatory for the AI studio flow:

- `sourceImageUrl`
- `prompt`
- `outputType`

For video generation specifically:

- `durationSeconds` should have a default
- frontend may omit it if backend default stays reliable

## Recommended backend rules

### Create project rules

`POST /api/v1/ai/projects` should require:

- `sourceImageUrl`
- `prompt`
- `outputType` as the preferred field

`generationType` can remain supported as a backward-compatible alias.

`contextLabel` can remain optional.

### Generate image rules

`POST /api/v1/ai/generate/image` should require:

- `projectId`
- prompt either:
  - already stored on the project, or
  - explicitly sent as an override
- source image either:
  - explicitly in `sourceImageUrl`, or
  - already stored on the project

### Generate video rules

`POST /api/v1/ai/generate/video` should require:

- `projectId`
- prompt either:
  - already stored on the project, or
  - explicitly sent as an override
- source image either:
  - explicitly in `sourceImageUrl`, or
  - already stored on the project

## What should not be used for this AI studio flow

The AI studio should not rely on the design-session flow as the primary contract for this requirement.

Avoid using these as the main AI studio path:

- `POST /api/v1/designs/sessions`
- `POST /api/v1/designs/sessions/{sessionId}/upload`
- `POST /api/v1/designs/sessions/{sessionId}/generate`

Reason:

- those routes are tied to design-session lifecycle and BOM/cart flow
- they currently require room dimensions
- they are not the cleanest fit for a prompt + image + output-type media generation experience

## Current gaps that need backend cleanup

To align the backend with this agreed flow, these are the immediate fixes needed:

### P0

- Update AI documentation so frontend stops using misleading payloads like `{ name, description }`
- Standardize the AI studio flow on:
  - upload image
  - create project
  - generate image or video
- Make it explicit that prompt and source image are both required inputs

### P1

- Tighten validation on `POST /api/v1/ai/projects` so prompt is not optional for this flow
- Tighten validation on `POST /api/v1/ai/generate/video` so frontend cannot send only `projectId`
- Align error payloads across AI endpoints

### P2

- Consider replacing the current 3-step flow with a future single endpoint such as:
  - `POST /api/v1/ai/generate`
- That future endpoint could accept:
  - uploaded image
  - prompt
  - output type
- But this is optional. The current endpoints can already support the target flow after cleanup.

## Recommended implementation stance

For the immediate next phase:

- keep `video` as the primary output mode
- require both prompt and uploaded image
- use AI project endpoints as the canonical AI studio flow
- treat image generation as a secondary branch of the same flow

That gives the frontend one clear mental model:

1. upload image
2. create AI project with prompt + output type
3. generate selected output

That is the cleanest alignment between product intent and backend behavior.
