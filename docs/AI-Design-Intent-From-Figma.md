# AI Design Intent From Figma

## Status

This note captures the intended meaning of the AI screens based on:

- the earlier Figma review already completed for this file
- the current frontend AI screen structure
- the product clarification you just gave

Important limitation:

- a fresh Figma MCP read was attempted again on 2026-04-06 and failed due to the connected Starter-plan MCP rate limit
- so this note reflects the best grounded interpretation from the already-reviewed Figma board and the clarified product direction

## What the AI screens are trying to be

The AI area is not just a loose prompt playground.

It is a guided AI design studio where the user gives the system enough context to generate a high-quality interior result.

That means the real design intent is:

- the user supplies a prompt
- the user supplies a before/reference image
- the system uses both together
- the user chooses the type of output they want
- video is the hero output
- image is a secondary but still valid output

## Product meaning of the AI experience

The AI screen appears to be about controlled transformation, not pure imagination.

In practical terms, that means:

- the uploaded image anchors the generation in the user's actual space or reference space
- the prompt tells the AI what style, mood, layout direction, and finish quality to apply
- the output selector lets the user decide whether they want:
  - a still redesign image
  - or a cinematic walkthrough-style video

So the design intent is accuracy plus inspiration, not random generation.

## Most likely intended user flow

## 1. Enter the AI design screen

The user lands on an AI studio/visualizer style page.

The page should communicate:

- upload your room or reference image
- describe what you want
- choose the output format

## 2. Provide context

The user provides:

- a prompt
- an uploaded image
- likely a room type or design context

These are not optional from a product standpoint if the goal is accurate output.

## 3. Choose output type

The user chooses either:

- image
- video

But the business priority is:

- video should be the default
- video should be treated as the premium/primary output path

## 4. Generate result

The system should then generate:

- an image redesign if image was selected
- a motion/cinematic room output if video was selected

## 5. Post-generation actions

The broader product likely expects the generated asset to feed into:

- design library/history
- save/favorite
- share
- download
- possibly follow-on commerce or project flows later

## What this means the screen is not

The AI screen is probably not intended to be:

- prompt-only text-to-image
- a BOM-first design-session workflow
- a hidden technical project creation flow exposed directly to the user

Those are backend or system concerns, not the user-facing mental model.

## Correct mental model for backend design

The backend should model this as a media-generation workflow with rich context.

The mental model should be:

1. upload source image
2. capture prompt and optional context
3. select output type
4. generate output
5. store project/result history

This is why the AI project flow is a better fit than the design-session/BOM flow for the AI screens.

## Design implications for backend contracts

If the backend is to align with what the Figma AI screens appear to want, then the canonical generation inputs should be:

- `sourceImageUrl`
- `prompt`
- `outputType`

Optional but useful:

- `contextLabel`
- `roomType`
- `durationSeconds` for video

`generationType` can remain as an internal or backward-compatible alias, but `outputType` is the clearer frontend-facing contract.

## Recommended canonical flow

### Upload image

`POST /api/v1/ai/upload-room`

### Create AI project

`POST /api/v1/ai/projects`

Canonical body:

```json
{
  "sourceImageUrl": "https://cdn.example.com/ai/source-room.png",
  "outputType": 2,
  "prompt": "Create a cinematic walkthrough of this living room with warm lighting and modern furniture styling",
  "contextLabel": "Living Room"
}
```

### Generate selected output

For image:

`POST /api/v1/ai/generate/image`

For video:

`POST /api/v1/ai/generate/video`

## Strongest interpretation of the design priority

The strongest product signal is this:

- the user should not have to think about internal backend flows
- the system should ask for enough creative context up front
- video should feel like the premium default experience
- image generation should remain available, but not define the whole AI architecture

## Bottom line

The Figma AI screens are best understood as:

- a context-rich AI studio
- powered by both prompt and image input
- with output-mode selection
- where video is the primary experience

That means the backend should align to one clear contract:

- prompt + image are first-class required inputs
- output type determines image or video output
- AI project endpoints should be the canonical flow behind the screen
