# AI Backend Refactor Checklist

## Goal

Align the backend to the AI design intent from Figma:

- user uploads a source image
- user enters a prompt
- user selects output type
- video is the default and primary experience
- image remains a supported additional output

## Canonical backend flow

1. `POST /api/v1/ai/upload-room`
2. `POST /api/v1/ai/projects`
3. `POST /api/v1/ai/generate/image` or `POST /api/v1/ai/generate/video`
4. `GET /api/v1/ai/projects`

## Completed in this pass

- `CreateAIProjectDto` now supports frontend-facing `outputType`
- `CreateAIProjectDto` still supports `generationType` as a backward-compatible alias
- AI project creation now requires:
  - `sourceImageUrl`
  - `prompt`
  - `outputType` or matching legacy `generationType`
- image generation now supports project-stored inputs with optional request overrides
- video generation now supports project-stored inputs with optional request overrides
- video generation now validates `durationSeconds > 0`
- AI project list responses now include `outputType`
- `POST /api/v1/ai/projects` now returns a cleaner project response DTO instead of the raw entity
- AI generation endpoints now return cleaner generation result DTOs instead of raw design entities
- `POST /api/v1/ai/upload-room` now returns a typed upload response DTO
- AI controller error payloads now consistently expose `message`
- AI flow docs were updated to reflect the canonical image/video flow

## Remaining backend work

### P0

- Add contract tests for:
  - project creation with `outputType = Video`
  - project creation with `outputType = Image`
  - generation using only `projectId`
  - generation using override `prompt`
  - generation using override `sourceImageUrl`
  - mismatched `outputType` and `generationType`
- Update any frontend proxy/helpers to use:
  - `outputType`
  - project-based generation payloads
- Review whether `POST /api/v1/ai/generate/video` should enforce a max duration

### P1

- Decide whether to add a single convenience endpoint such as:
  - `POST /api/v1/ai/generate`
- If added, it should dispatch from project/output type and reduce frontend branching
- Consider adding explicit Swagger summaries/examples for the AI studio endpoints
- Normalize AI enum serialization if frontend wants string enums instead of numeric values

### P2

- Decide whether `contextLabel` should become a richer typed field like `roomType`
- Consider storing additional creative metadata for future prompt history or regeneration
- Consider job-status polling if video generation becomes long-running

## DTO and controller implications

### Project creation

Preferred request:

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "outputType": 2,
  "prompt": "Create a cinematic walkthrough of this room with warm lighting and modern furniture styling",
  "contextLabel": "Living Room"
}
```

### Image generation

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
  "prompt": "Create a brighter, warmer version",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room-v2.png"
}
```

### Video generation

Minimum request:

```json
{
  "projectId": "22222222-2222-2222-2222-222222222222",
  "durationSeconds": 9
}
```

Optional override request:

```json
{
  "projectId": "22222222-2222-2222-2222-222222222222",
  "prompt": "Create a cinematic walkthrough with softer lighting",
  "sourceImageUrl": "https://cdn.example.com/ai/source-room-v2.png",
  "durationSeconds": 9
}
```

## Acceptance check

The refactor is functionally acceptable for this phase when:

1. frontend can create either image or video projects using `outputType`
2. frontend can keep video as default without losing image support
3. generation works without resending prompt/image every time
4. error responses expose a readable `message`
5. docs and controller behavior agree on the same flow
