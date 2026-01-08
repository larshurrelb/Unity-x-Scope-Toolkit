<div align="center">
  <img src="ReadMeCover.png" alt="Unity x Scope Toolkit" width="300">
</div>

# Unity x Scope Toolkit

A Unity toolkit for creating **real-time AI-stylized video experiences** using [Scope's LongLive](https://github.com/ScopeFoundry/Scope) API via WebRTC streaming.

![Unity](https://img.shields.io/badge/Unity-6000.0+-black?logo=unity)
![URP](https://img.shields.io/badge/URP-Required-blue)

## What It Does

Captures gameplay footage (with depth extraction and character masking), streams it to RunPod's LongLive API, and displays AI-transformed output in real-time. Supports dynamic prompts based on player state, interactive NPC chat via Google Gemini, and location-based prompt zones.

---

## Requirements

| Requirement | Details |
|-------------|---------|
| **Unity** | 6000.0.5812+ with Universal Render Pipeline |
| **RunPod** | Scope LongLive endpoint (recommended: RTX 5090 or 6000 Pro) |
| **Gemini API Key** | [Get free key](https://aistudio.google.com/app/apikey) (for NPC chat) |

---

## Quick Setup

1. **Clone & Open** in Unity Hub
2. **Import Required Assets:**
   - [Starter Assets - Third Person](https://assetstore.unity.com/packages/essentials/starter-assets-thirdperson-updates-in-new-charactercontroller-pa-196526) from Package Manager → My Assets
   - Click **Import TMP Essentials** if prompted
3. **Open** `Assets/Scenes/Default Scene.unity`
4. **Configure API Keys** in the Setup Screen UI or Inspector:
   - `DaydreamAPIManager` → RunPod URL
   - `GeminiChatManager` → Gemini API Key
5. **Press Play**

---

## Key Scripts

### Core Managers (`Assets/Scripts/`)

| Script | Purpose |
|--------|---------|
| `DaydreamAPIManager.cs` | WebRTC streaming to RunPod, parameter updates via DataChannel |
| `PromptManager.cs` | Combines prefix + action + suffix, auto-updates based on player state |
| `GeminiChatManager.cs` | Google Gemini API integration for NPC conversations |
| `InputSwitcher.cs` | Manages input sources, UI modes (Start/Gameplay/Chat) |

### Prompt Zones

| Script | Purpose |
|--------|---------|
| `PrefixChanger.cs` | Changes prompt prefix when player enters zone |
| `SuffixChanger.cs` | Changes prompt suffix when player enters zone |
| `PromptReplacer.cs` | Replaces entire prompt + noise scale in zone |
| `CacheReset.cs` | Resets AI generation cache on zone entry |
| `CharacterChat.cs` | NPC chat trigger with custom AI personality |

### Depth & Visualization

| Script | Purpose |
|--------|---------|
| `DepthColorToTexture.cs` | Dual-camera depth extraction with character masking |
| `OpenPoseSkeletonRenderer.cs` | Skeleton visualization for AI guidance |
| `MinimapToggle.cs` | Top-down minimap mode |

---

## Keyboard Controls

| Key | Action |
|-----|--------|
| `1` | Toggle video input source |
| `2` | Toggle minimap mode |
| `3` | Toggle OpenPose skeleton |
| `0` | Reset API cache |
| `Tab` | Toggle parameter UI |
| `Space` | Start game (from start screen) |
| `F` | Start NPC chat (when near character) |
| `Esc` | Exit chat mode |
| `WASD` | Move character |
| `Q/E` | Rotate camera |
| `R` | Reset camera |

---

## APIs Required

| API | Purpose | Where to Get |
|-----|---------|--------------|
| **RunPod LongLive** | Real-time video-to-video AI | Deploy Scope on RunPod |
| **Google Gemini** | NPC chat conversations | [Google AI Studio](https://aistudio.google.com/app/apikey) |

---

## Editor Tools

- **Daydream → Create WebRTC-Compatible RenderTexture** - Creates textures with correct B8G8R8A8_SRGB format

---

## Documentation

See `.github/copilot-instructions.md` for detailed API documentation and architecture overview.

---

## License

Part of [Scope Workshop 25](https://github.com/ScopeFoundry/Scope). Third-party assets (Starter Assets) must be obtained separately from the Unity Asset Store.