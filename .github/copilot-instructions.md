# Daydream Live Unity Integration - AI Agent Instructions

## Project Overview

**Daydream Live** is a Unity URP project that creates **real-time AI-stylized video experiences**. It captures gameplay footage (with depth extraction and character masking), streams it to RunPod's LongLive API via WebRTC, and displays the AI-transformed output in real-time. The system supports dynamic prompt updates based on player state, interactive NPC chat with Google Gemini, and location-based prompt zones.

**Key Features:**
- Real-time video-to-video AI stylization via WebRTC streaming
- Depth-based visualization with character layer isolation
- Dynamic prompts that react to player movement (walking, running, jumping)
- Interactive AI character conversations using Google Gemini API
- Location-based trigger zones for prompt customization
- Start screen, gameplay, and chat UI modes with smooth transitions

**Built for:** Unity 2022.3+ with Universal Render Pipeline (URP)

---

## System Architecture

### High-Level Data Flow
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              UNITY GAME                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐    ┌──────────────────┐    ┌─────────────────────────┐   │
│  │ Game Camera  │───▶│ DepthColorTo     │───▶│ Input RenderTexture     │   │
│  │ + Character  │    │ Texture.cs       │    │ (B8G8R8A8_SRGB)         │   │
│  │   Masking    │    │ (depth+char mask)│    │                         │   │
│  └──────────────┘    └──────────────────┘    └───────────┬─────────────┘   │
│                                                          │                  │
│  ┌──────────────┐                                        ▼                  │
│  │PromptManager │─────────────────────────────▶ ┌─────────────────────┐    │
│  │ (dynamic     │                               │ DaydreamAPIManager  │    │
│  │  prompts)    │                               │ (WebRTC streaming)  │    │
│  └──────────────┘                               └─────────┬───────────┘    │
│         ▲                                                 │                 │
│  ┌──────┴───────┐                                        │                 │
│  │ Trigger Zones│                                        ▼                 │
│  │ (Prefix/     │                             ┌─────────────────────┐      │
│  │  Suffix/     │                             │ RunPod LongLive API │      │
│  │  Replacer)   │                             │ (AI Video Pipeline) │      │
│  └──────────────┘                             └─────────┬───────────┘      │
│                                                         │                   │
│  ┌──────────────────┐                                   ▼                  │
│  │ GeminiChatManager│◀────────────────────▶  ┌─────────────────────┐      │
│  │ (character chat) │                        │ Output RenderTexture│      │
│  └──────────────────┘                        │ (AI-stylized video) │      │
│                                              └─────────────────────┘       │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Three-Component System
1. **Input Pipeline**: Camera → Depth Extraction + Character Masking → Video Texture (B8G8R8A8_SRGB format required)
2. **API Manager**: WebRTC bidirectional streaming to RunPod + real-time parameter updates via DataChannel
3. **Output Display**: Render received AI-processed video to texture/UI

---

## Scripts Reference

### Core Scripts (`Assets/Scripts/`)

#### DaydreamAPIManager.cs
**Purpose:** WebRTC orchestrator for RunPod LongLive API communication.

**Key Features:**
- Manages WebRTC peer connection with video track + data channel
- Handles pipeline loading, offer/answer negotiation, ICE candidates
- Sends real-time parameter updates (prompts, noise scale, denoising steps)
- Auto-starts streaming on `Start()`

**Inspector Fields:**
- `runPodBaseUrl`: RunPod proxy URL endpoint
- `inputVideoTexture`: Source RenderTexture to stream (must be B8G8R8A8_SRGB)
- `outputVideoTexture`: Target RenderTexture for received AI video
- `prompt`: Base prompt text for AI generation
- `width/height`: Video resolution (default: 576x320)
- `seed`: Random seed for consistent generation
- `denoisingSteps`: Array of denoising step values (e.g., [1000, 750, 500, 250])
- `noiseScale`: 0-1, higher = more AI variation, lower = more faithful to input

**Public Methods:**
- `StartStreaming()` / `StopStreaming()`: Control streaming lifecycle
- `UpdatePrompt(string, float)`: Update prompt text and weight
- `SetNoiseScale(float)`: Update noise scale (0-1)
- `UpdatePromptAndNoiseScale(string, float, float)`: Batched update (more efficient)
- `ResetCache()`: Reset video generation cache (use when scene changes significantly)
- `SetDenoisingSteps(int[])`: Update denoising step array
- `SendCustomParameters(RunPodParameters)`: Send custom parameter object

---

#### PromptManager.cs
**Purpose:** High-level prompt management with character state tracking.

**Key Features:**
- Combines prefix + action + suffix into full prompts
- Auto-detects player state (standing, walking, sprinting, jumping) via `ThirdPersonController`
- Dynamic noise scaling based on movement
- Smooth transitions between noise scale values

**Inspector Fields:**
- `promptPrefix/promptSuffix`: Static prompt parts
- `standingAction/walkingAction/sprintingAction/jumpingAction`: Action descriptions per state
- `noiseScale`: Static noise scale (or base for dynamic)
- `autoUpdate`: Enable/disable automatic state-based updates
- `updateCooldown`: Minimum time between API updates
- `useDynamicNoise`: Enable movement-based noise scaling
- `standingNoiseScale/movingNoiseScale`: Noise values for different states

**Public Methods:**
- `SetPrefix(string)` / `SetSuffix(string)`: Update prompt parts
- `SetAction(string)`: Override action text
- `SetNoiseScale(float)`: Update noise scale
- `SetAutoUpdate(bool)`: Toggle auto-update behavior
- `GetPrefix()` / `GetSuffix()` / `GetDefaultPrefix()` / `GetDefaultSuffix()`: Retrieve values
- `ResetCache()`: Forward cache reset to API manager
- `UpdatePrompt()`: Manually trigger prompt update

---

#### InputSwitcher.cs
**Purpose:** Manages input sources, UI modes, and game state transitions.

**Key Features:**
- Keyboard switching between 3 video texture sources (keys 1/2/3)
- Three UI modes: Start Screen, Gameplay, Chat
- Smooth UI transitions with animation
- Player controller enable/disable during modes
- Cursor lock/unlock management

**Inspector Fields:**
- `videoTexture1/2/3`: Source RenderTextures to switch between
- `outputTexture`: Target RenderTexture for output
- `startScreenUI/gameplayUI/chatbotUI`: UI GameObjects for each mode
- `playerController`: ThirdPersonController reference
- `transitionScreen`: Transition animation GameObject
- `startScreenPrompt`: Prompt used during start screen

**Public Methods:**
- `SetChatMode(bool)`: Enter/exit chat mode
- `ToggleChatMode()`: Toggle chat mode state
- `IsChatMode`: Property to check current mode
- `IsStartScreen`: Property to check if on start screen

**Keyboard Controls:**
- `1/2/3`: Switch video input source
- `0`: Reset API cache
- `Tab`: Toggle video input preview visibility
- `Space`: Start game from start screen
- `Escape`: Exit chat mode

---

#### GeminiChatManager.cs
**Purpose:** Manages interactive chat with Google Gemini API.

**Key Features:**
- Real-time chat with Gemini AI models
- Typewriter effect for responses
- Conversation history management
- Emotion/gesture parsing from AI responses for visual feedback
- Events for typing start/finish (used by CharacterChat)

**Inspector Fields:**
- `apiKey`: Google Gemini API key
- `modelName`: Gemini model (default: "gemini-3-flash-preview")
- `systemPrompt`: Default AI personality/behavior instructions
- `outputText`: TMP_Text for conversation display
- `inputField`: TMP_InputField for user messages
- `temperature`: Response creativity (0-2)
- `maxOutputTokens`: Max response length
- `maintainHistory`: Keep conversation context
- `typingSpeed`: Typewriter effect speed

**Public Methods:**
- `SendMessage(string)`: Send user message to Gemini
- `SetSystemPrompt(string)`: Update AI personality
- `ClearConversation()`: Reset conversation history
- `SendGreeting()`: Trigger AI introduction

**Events:**
- `OnTypingStarted(string emotion, string gesture)`: AI began responding
- `OnTypingFinished`: AI finished responding

---

#### CharacterChat.cs
**Purpose:** Location-based NPC chat trigger with image generation customization.

**Key Features:**
- Trigger zone that activates when player enters
- Customizes image generation prompts during conversation
- Integrates Gemini chat with character-specific system prompts
- Updates AI image based on character emotion/gesture during chat
- Restores original settings on exit

**Inspector Fields:**
- `replacementPrompt`: Image generation prompt for this character
- `replacementNoiseScale`: Noise scale during conversation
- `characterName`: Display name in chat UI
- `characterSystemPrompt`: AI personality for this character
- `interactionPromptText`: "Press E to chat" indicator
- `disableAutoUpdate`: Stop movement-based prompt changes during chat
- `restoreOnExit`: Return to normal settings when leaving

**Public Methods:**
- `SetCharacterSystemPrompt(string)`: Update AI personality at runtime
- `SetReplacementPrompt(string)`: Update image prompt at runtime
- `IsPlayerInside()` / `IsInChatMode()`: State queries

---

### Depth Extraction Scripts

#### DepthColorToTexture.cs
**Purpose:** Advanced depth visualization with character masking.

**Key Features:**
- Dual-camera system: main camera for depth, child camera for character mask
- Configurable depth range remapping
- Three-color output: near depth, far depth, character color
- Layer-based character isolation

**Inspector Fields:**
- `targetCamera`: Main camera to extract depth from
- `depthTexture`: Output RenderTexture
- `depthMaterial`: Material using DepthColorShader
- `excludeLayers`: Layers to exclude from depth (typically character)
- `characterLayers`: Layers to render as character color

---

#### DepthToTexture.cs
**Purpose:** Simple depth-only extraction without character masking.

**Inspector Fields:**
- `targetCamera`: Camera to extract depth from
- `depthTexture`: Output RenderTexture
- `depthMaterial`: Material with depth shader

---

### Trigger Zone Scripts

#### PrefixChanger.cs
**Purpose:** Changes prompt prefix when player enters trigger zone.

**Inspector Fields:**
- `newPrefix`: Prefix to apply in this zone
- `appendToExisting`: Add to existing prefix vs replace
- `restoreOnExit`: Restore original on exit

---

#### SuffixChanger.cs
**Purpose:** Changes prompt suffix when player enters trigger zone.

**Inspector Fields:**
- `newSuffix`: Suffix to apply in this zone
- `restoreOnExit`: Restore original on exit

---

#### PromptReplacer.cs
**Purpose:** Completely replaces prompt when player enters trigger zone.

**Inspector Fields:**
- `replacementPrompt`: Complete prompt for this zone
- `replacementNoiseScale`: Override noise scale
- `replacementDenoisingStep2`: Override denoising step at index 2
- `disableAutoUpdate`: Stop auto-updates in zone
- `restoreOnExit`: Restore original on exit

---

#### CacheReset.cs
**Purpose:** Resets API cache when player enters trigger zone.

**Inspector Fields:**
- `resetOnce`: Only reset once per session
- `playerTag`: Tag to detect for player

---

### Utility Scripts

#### FadeRawImage.cs
**Purpose:** Fades out a RawImage after a delay, then deactivates it.

**Inspector Fields:**
- `waitSeconds`: Delay before fade starts
- `fadeDuration`: Duration of fade animation

---

#### CloseTransition.cs
**Purpose:** Auto-deactivates GameObject when its animation completes.

---

### Editor Scripts (`Assets/Scripts/Editor/`)

#### DaydreamTextureHelper.cs
**Purpose:** Editor window for creating WebRTC-compatible RenderTextures.

**Menu Access:** `Daydream → Create WebRTC-Compatible RenderTexture`

Creates RenderTextures with correct B8G8R8A8_SRGB format required for WebRTC streaming.

---

## Shader Reference (`Assets/Shaders and Materials/`)

### DepthColorShader.shader
**Path:** `Custom/DepthColorShader`

URP HLSL shader for depth visualization with character masking.

**Properties:**
- `_DepthMin/_DepthMax`: Depth range remapping
- `_Invert`: Invert depth values
- `_ColorNear/_ColorFar`: Depth gradient colors
- `_ColorCharacter`: Color for masked characters
- `_CharacterMask`: Character mask texture input

### CharacterMaskShader.shader
**Path:** `Hidden/CharacterMask`

Replacement shader that renders all geometry as solid white. Used by CharacterMaskCamera to create binary character masks.

### DepthShader.shader
Simple depth visualization shader (grayscale).

### Black and White Shader.shader
Converts input to grayscale.

---

## Scenes (`Assets/Scenes/`)

- **Blade Runner.unity**: Cyberpunk-themed environment
- **Grassy Fields.unity**: Outdoor natural environment
- **Playground 1.unity**: Testing/development scene
- **SampleScene.unity**: Basic sample scene
- **Third Party Scene.unity**: Third-party assets scene

---

## URP Settings (`Assets/Settings/`)

- **Mobile_RPAsset.asset** / **Mobile_Renderer.asset**: Mobile render pipeline configuration
- **PC_RPAsset.asset** / **PC_Renderer.asset**: Desktop render pipeline configuration
- **DefaultVolumeProfile.asset**: Default post-processing volume
- **SampleSceneProfile.asset**: Scene-specific volume profile

**Critical Setting:** Both URP assets must have `m_RequireDepthTexture: 1` enabled for depth extraction.

---

## Critical Requirements

### RenderTexture Format
**ALL video textures for WebRTC streaming MUST use B8G8R8A8_SRGB format.** Unity WebRTC enforces this. The DaydreamAPIManager validates format and throws descriptive errors. Use editor tool: `Daydream → Create WebRTC-Compatible RenderTexture`.

### Depth Setup
URP depth texture requires:
1. Set `cameraData.requiresDepthTexture = true` (via `GetUniversalAdditionalCameraData()`)
2. Enable in URP Asset: `m_RequireDepthTexture: 1`
3. Cameras must render with depth buffer (16/24/32 bit)

### Layer Masking Pattern
Character isolation uses layered rendering:
- Main camera: `originalCullingMask & ~excludeLayers` (depth only for non-characters)
- Child CharacterMaskCamera: renders only `characterLayers` with replacement shader to R8 texture
- Shader combines both: `lerp(depthColor, _ColorCharacter, characterMask)`

---

## Development Workflows

### Setting Up a New Scene
1. Add `DaydreamAPIManager` to scene with RunPod URL configured
2. Create WebRTC-compatible RenderTextures for input/output (use editor tool)
3. Add `PromptManager` and link to API manager
4. Add `InputSwitcher` for input source management
5. Set up camera with `DepthColorToTexture` for depth extraction
6. Configure UI GameObjects for start screen, gameplay, and chat modes
7. Add trigger zones with `PrefixChanger`, `SuffixChanger`, or `PromptReplacer` as needed

### Adding an NPC Character
1. Create trigger collider around NPC area
2. Add `CharacterChat` component to trigger GameObject
3. Configure `characterName` and `characterSystemPrompt` for AI personality
4. Set `replacementPrompt` for image generation style during conversation
5. Link `interactionPromptText` for "Press E to chat" UI
6. Optionally set custom `replacementNoiseScale` and `replacementDenoisingStep2`

### Testing WebRTC Connection
1. Update `runPodBaseUrl` in DaydreamAPIManager inspector
2. Assign input/output RenderTextures (verify B8G8R8A8_SRGB format)
3. Enter Play mode - streaming auto-starts, watch Console for `[RunPod]` logs
4. Pipeline loading takes ~30s (polls status every 2s, max 60 attempts)
5. Toggle `updateParameters` boolean in inspector to push new prompts while streaming

---

## API Integration Patterns

### WebRTC Workflow (DaydreamAPIManager)
```
Start → LoadPipeline (POST /api/v1/pipeline/load, poll /status) 
     → GetICEServers (GET /api/v1/webrtc/ice-servers)
     → SetupWebRTC (create RTCPeerConnection, add video track + data channel)
     → CreateOffer → POST /api/v1/webrtc/offer with initialParameters
     → SetRemoteDescription (answer) → PATCH /offer/{sessionId} with ICE candidates
     → Streaming active, data channel sends JSON parameter updates
```

### Parameter Update JSON Structure
```json
{
  "input_mode": "video",
  "prompts": [{"text": "...", "weight": 1.0}],
  "prompt_interpolation_method": "slerp",
  "denoising_step_list": [1000, 750, 500, 250],
  "noise_scale": 0.8,
  "manage_cache": true
}
```

### Cache Reset JSON
```json
{
  "manage_cache": false,
  "reset_cache": true
}
```

---

## Unity-Specific Conventions

### Coroutine Management
- WebRTC requires `StartCoroutine(WebRTC.Update())` in Start() - critical for peer connection lifecycle
- Store coroutine references for cleanup: `streamingCoroutine = StartCoroutine(StreamingWorkflow())`
- Always cleanup in OnDestroy: dispose peer connections, tracks, data channels

### URP Shader Includes
```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
```
Use `SampleSceneDepth(uv)` and `Linear01Depth(depth, _ZBufferParams)` for depth sampling.

### Camera Synchronization
Child cameras must sync transform + settings in LateUpdate:
```csharp
characterCamera.fieldOfView = targetCamera.fieldOfView;
characterCamera.nearClipPlane = targetCamera.nearClipPlane;
// ...sync in LateUpdate() to ensure parent updates first
```

---

## Common Pitfalls

1. **JsonUtility Limitations**: Cannot serialize top-level lists/dictionaries. Wrap in [Serializable] class. All nested classes need [Serializable] attribute.

2. **Depth Texture Availability**: Check URP asset settings AND runtime camera config. Depth won't be available if either is misconfigured.

3. **WebRTC Texture Lifecycle**: VideoStreamTrack.Texture ownership - don't Release() manually, let WebRTC manage. Use Graphics.Blit() in coroutine loop for safe copying.

4. **Replacement Shader Tags**: CharacterMaskCamera uses `SetReplacementShader(shader, "")` - empty tag matches all renderables. Character shader must be at `Hidden/CharacterMask`.

5. **Aspect Ratio**: DepthColorToTexture sets camera aspect based on depth texture dimensions. Override if different aspect needed.

6. **Auto-Find Pattern**: Most scripts auto-find references (PromptManager, InputSwitcher, etc.) if not assigned. Check console for warnings about missing references.

7. **Player Tag**: Trigger zones expect "Player" tag by default. Configure `playerTag` field if using different tag.

---

## Package Dependencies

- **Unity WebRTC**: 3.0.0 - WebRTC implementation for streaming
- **Universal RP**: 17.2.0 - Render pipeline
- **Input System**: 1.14.2 - New input system for controls
- **TextMeshPro**: UI text rendering

---

## File References

- Setup guides: `CHARACTER_LAYER_SETUP.md`, `DEPTH_EXTRACTION_SETUP.md`, `TEXTURE_FORMAT_FIX.md`, `VIDEO_INPUT_SWITCHER_GUIDE.md`
- Editor tools: `Assets/Scripts/Editor/DaydreamTextureHelper.cs`
- API documentation: `Assets/Scripts/DaydreamAPIManager-SD-Turbo.md`

IMPORTANT: This project is released as a toolkit for others to use as a public github repository. This means its important no API Keys are in the code, no name of the user leaks and no proprietary information is shared. 

Code should be commented to help users understand how to use and modify the toolkit for their own purposes. The only markdown for users should be the readme file. All other markdown files for internal use (as in for development) should land in the Assets/NotinGit folder. 