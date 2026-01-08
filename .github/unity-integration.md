# Unity Integration Guide for Daydream Scope (LongLive Pipeline)

This documentation covers how to connect a Unity application to a Daydream Scope server running on RunPod, specifically for the **LongLive** pipeline.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [API Endpoints Reference](#api-endpoints-reference)
3. [Connection Workflow](#connection-workflow)
4. [Pipeline Load Parameters](#pipeline-load-parameters)
5. [WebRTC Initial Parameters](#webrtc-initial-parameters)
6. [Data Channel Runtime Parameters](#data-channel-runtime-parameters)
7. [Cache Management](#cache-management)
8. [Prompt Configuration](#prompt-configuration)
9. [Complete Unity Example](#complete-unity-example)
10. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

The Scope server uses WebRTC for bidirectional video streaming:

```
┌─────────────────┐       WebRTC        ┌─────────────────┐
│   Unity App     │◄──────────────────►│  Scope Server   │
│                 │                     │   (RunPod)      │
│ - Send video    │    Data Channel     │                 │
│ - Receive video │◄──────────────────►│ - LongLive      │
│ - Send params   │   (JSON messages)   │   Pipeline      │
└─────────────────┘                     └─────────────────┘
```

**Key Components:**
- **REST API**: Pipeline loading, status checks, WebRTC signaling
- **WebRTC**: Video streaming (send input, receive generated output)
- **Data Channel**: Real-time parameter updates (prompts, cache reset, etc.)

---

## API Endpoints Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/pipeline/status` | GET | Check current pipeline status |
| `/api/v1/pipeline/load` | POST | Load/reload a pipeline |
| `/api/v1/webrtc/ice-servers` | GET | Get ICE/TURN server configuration |
| `/api/v1/webrtc/offer` | POST | Send WebRTC offer, receive answer |
| `/api/v1/webrtc/offer/{sessionId}` | PATCH | Send ICE candidates (Trickle ICE) |
| `/health` | GET | Health check endpoint |

---

## Connection Workflow

### Step 1: Check/Load Pipeline

**Check Status:**
```http
GET /api/v1/pipeline/status
```

**Response:**
```json
{
  "status": "loaded",           // "not_loaded", "loading", "loaded", "error"
  "pipeline_id": "longlive",
  "load_params": {
    "height": 320,
    "width": 576,
    "seed": 42
  },
  "loaded_lora_adapters": []
}
```

**Load Pipeline (if needed):**
```http
POST /api/v1/pipeline/load
Content-Type: application/json

{
  "pipeline_id": "longlive",
  "load_params": {
    "height": 320,
    "width": 576,
    "seed": 42
  }
}
```

> ⚠️ **IMPORTANT**: Pipeline loading is **asynchronous**. You must poll `/api/v1/pipeline/status` until `status == "loaded"` before proceeding with WebRTC connection.

### Step 2: Get ICE Servers

```http
GET /api/v1/webrtc/ice-servers
```

**Response:**
```json
{
  "iceServers": [
    {
      "urls": ["turn:example.com:3478"],
      "username": "user",
      "credential": "pass"
    }
  ]
}
```

### Step 3: WebRTC Offer/Answer

```http
POST /api/v1/webrtc/offer
Content-Type: application/json

{
  "sdp": "<local SDP offer>",
  "type": "offer",
  "initialParameters": {
    "input_mode": "video",
    "prompts": [{ "text": "A cyberpunk cityscape", "weight": 1.0 }],
    "prompt_interpolation_method": "slerp",
    "denoising_step_list": [1000, 750, 500, 250],
    "manage_cache": true
  }
}
```

**Response:**
```json
{
  "sdp": "<server SDP answer>",
  "type": "answer",
  "sessionId": "uuid-string"
}
```

### Step 4: Trickle ICE Candidates

```http
PATCH /api/v1/webrtc/offer/{sessionId}
Content-Type: application/json

{
  "candidates": [
    {
      "candidate": "candidate:...",
      "sdpMid": "0",
      "sdpMLineIndex": 0
    }
  ]
}
```

---

## Pipeline Load Parameters

These are set when loading the pipeline and **require a pipeline reload** to change:

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `height` | int | 320 | 16-2048 | Output video height in pixels |
| `width` | int | 576 | 16-2048 | Output video width in pixels |
| `seed` | int | 42 | ≥0 | Random seed for reproducibility |
| `quantization` | string/null | null | "fp8_e4m3fn" or null | Model quantization (reduces VRAM, slight quality loss) |
| `loras` | array | [] | - | LoRA adapters to load (see LoRA docs) |
| `lora_merge_mode` | string | "permanent_merge" | "permanent_merge" or "runtime_peft" | How LoRAs are applied |

**Recommended Resolutions for LongLive:**
- **480×832** - Training resolution, best quality
- **320×576** - Faster generation, good for testing
- **512×512** - Standard square format (for video input mode)

### Unity C# Example:
```csharp
[Serializable]
public class PipelineLoadRequest
{
    public string pipeline_id = "longlive";
    public PipelineLoadParams load_params;
}

[Serializable]
public class PipelineLoadParams
{
    public int height = 320;
    public int width = 576;
    public int seed = 42;
    public string quantization = null; // or "fp8_e4m3fn"
}
```

---

## WebRTC Initial Parameters

Sent with the WebRTC offer to configure the initial stream state:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `input_mode` | string | "text" | `"text"` for T2V, `"video"` for V2V |
| `prompts` | array | - | Array of prompt objects (see below) |
| `prompt_interpolation_method` | string | "linear" | `"linear"` or `"slerp"` for blending |
| `denoising_step_list` | int[] | [1000,750,500,250] | Denoising schedule (descending) |
| `manage_cache` | bool | true | Auto-manage temporal cache |
| `noise_scale` | float | 0.7 (video mode) | Noise amount (0.0-1.0, video mode only) |
| `noise_controller` | bool | true (video mode) | Auto-adjust noise based on motion |

### Unity C# Example:
```csharp
[Serializable]
public class InitialParameters
{
    public string input_mode = "video";  // or "text"
    public List<PromptData> prompts = new List<PromptData>();
    public string prompt_interpolation_method = "slerp";
    public int[] denoising_step_list = new int[] { 1000, 750, 500, 250 };
    public bool manage_cache = true;
    public float noise_scale = 0.7f;       // Only for video mode
    public bool noise_controller = true;   // Only for video mode
}

[Serializable]
public class PromptData
{
    public string text;
    public float weight;
}
```

---

## Data Channel Runtime Parameters

These can be sent via the WebRTC Data Channel **during streaming** to update generation in real-time:

| Parameter | Type | Description |
|-----------|------|-------------|
| `prompts` | array | Update active prompts |
| `prompt_interpolation_method` | string | Change blending method |
| `transition` | object | Smooth prompt transition (see below) |
| `denoising_step_list` | int[] | Update denoising schedule |
| `manage_cache` | bool | Toggle auto cache management |
| `reset_cache` | bool | **Force cache reset** (one-shot trigger) |
| `paused` | bool | Pause/resume generation |
| `noise_scale` | float | Adjust noise (video mode) |
| `noise_controller` | bool | Toggle auto noise adjustment |
| `lora_scales` | object | Adjust LoRA strengths at runtime |

### Prompt Transition Object

For smooth transitions between prompts:

```json
{
  "transition": {
    "target_prompts": [
      { "text": "New scene description", "weight": 1.0 }
    ],
    "num_steps": 4,
    "temporal_interpolation_method": "slerp"
  }
}
```

---

## Cache Management

The LongLive pipeline uses a **temporal cache** to maintain coherence between generated frames. Understanding cache management is crucial for good results.

### `manage_cache` (boolean)

When **enabled** (`true`, default):
- The pipeline automatically manages the cache
- Cache is reset automatically when parameters change (e.g., denoising steps)
- **You cannot manually reset the cache** while this is enabled

When **disabled** (`false`):
- You have manual control over cache resets
- Use `reset_cache: true` to force a reset
- Useful for precise control over generation continuity

### `reset_cache` (boolean)

**This is how you reset the cache from Unity!**

Sending `reset_cache: true` via the Data Channel:
- Clears the temporal cache
- Clears the output buffer queue
- Next frames will start fresh (like beginning of a new video)
- This is a **one-shot trigger** (no need to set it back to false)

> ⚠️ **IMPORTANT**: `reset_cache` only works when `manage_cache` is `false`. If `manage_cache` is `true`, the pipeline ignores manual reset requests.

### Unity C# Implementation for Cache Reset:

```csharp
/// <summary>
/// Reset the video generation cache.
/// Call this when you want to start a fresh video sequence.
/// </summary>
public void ResetCache()
{
    if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
    {
        Debug.LogWarning("[RunPod] Data channel not open, cannot reset cache.");
        return;
    }

    // First, disable auto cache management if it's enabled
    // Then send the reset command
    string json = "{\"manage_cache\": false, \"reset_cache\": true}";
    Debug.Log($"[RunPod] Resetting cache: {json}");
    dataChannel.Send(json);
}

/// <summary>
/// Reset cache and optionally re-enable auto management.
/// </summary>
public void ResetCacheAndRestore()
{
    if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
    {
        Debug.LogWarning("[RunPod] Data channel not open, cannot reset cache.");
        return;
    }

    // Disable manage_cache, reset, then you can re-enable in a subsequent call
    string resetJson = "{\"manage_cache\": false, \"reset_cache\": true}";
    dataChannel.Send(resetJson);

    // Optionally re-enable after a brief delay
    StartCoroutine(ReenableManageCache());
}

private IEnumerator ReenableManageCache()
{
    yield return new WaitForSeconds(0.5f);
    if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
    {
        string json = "{\"manage_cache\": true}";
        dataChannel.Send(json);
    }
}
```

### When to Reset Cache

- **Scene change**: When switching to a completely different prompt/scene
- **Timeline rewind**: When jumping backwards in a timeline
- **Fresh start**: When you want the AI to "forget" previous context
- **After errors**: If the video looks corrupted or stuck

---

## Prompt Configuration

### Single Prompt

```csharp
var parameters = new RunPodParameters
{
    prompts = new List<PromptData>
    {
        new PromptData { text = "A panda walking in a forest", weight = 1.0f }
    }
};
```

### Multiple Prompts (Blended)

Prompts with weights allow spatial blending:

```csharp
var parameters = new RunPodParameters
{
    prompts = new List<PromptData>
    {
        new PromptData { text = "A sunny day", weight = 0.7f },
        new PromptData { text = "Rain falling", weight = 0.3f }
    },
    prompt_interpolation_method = "slerp"  // or "linear"
};
```

### Interpolation Methods

| Method | Description | Best For |
|--------|-------------|----------|
| `linear` | Linear interpolation in embedding space | Simple blends |
| `slerp` | Spherical linear interpolation | Smoother transitions, preserves magnitude |

### Prompt Tips for LongLive

1. **Include subject and setting**: "A **panda** walks along a path in a **park**"
2. **Use detailed prompts**: Longer, more descriptive prompts work better
3. **Maintain anchors**: Keep consistent subject/setting references for continuity
4. **Cinematic transitions**: Works better with slow camera movements than rapid cuts

---

## Complete Unity Example

Here's an enhanced version of your script with cache reset functionality:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;
using System.Text;

public class DaydreamAPIManager : MonoBehaviour
{
    #region Data Structures

    [Serializable]
    public class RuntimeParameters
    {
        // Prompts
        public List<PromptData> prompts;
        public string prompt_interpolation_method;

        // Denoising
        public int[] denoising_step_list;

        // Cache control
        public bool? manage_cache;
        public bool? reset_cache;

        // Video mode controls
        public float? noise_scale;
        public bool? noise_controller;

        // Playback control
        public bool? paused;
    }

    [Serializable]
    public class PromptData
    {
        public string text;
        public float weight = 1.0f;
    }

    #endregion

    #region Inspector Fields

    [Header("API Configuration")]
    [SerializeField] private string runPodBaseUrl = "https://your-runpod-url:8000";

    [Header("Pipeline Settings")]
    [SerializeField] private int width = 576;
    [SerializeField] private int height = 320;
    [SerializeField] private int seed = 42;

    [Header("Generation Settings")]
    [SerializeField] private bool useVideoMode = false;
    [SerializeField] private int[] denoisingSteps = { 1000, 750, 500, 250 };
    [SerializeField] [Range(0f, 1f)] private float noiseScale = 0.7f;
    [SerializeField] private bool noiseController = true;
    [SerializeField] private bool manageCache = true;

    [Header("Prompt")]
    [TextArea(3, 10)]
    [SerializeField] private string currentPrompt = "A 3D animated scene. A panda walks towards the camera.";

    #endregion

    #region Private Fields

    private RTCDataChannel dataChannel;
    private bool isStreaming = false;

    #endregion

    #region Public Methods - Cache Control

    /// <summary>
    /// Reset the video generation cache.
    /// Use this when starting a new scene or after timeline rewind.
    /// </summary>
    [ContextMenu("Reset Cache")]
    public void ResetCache()
    {
        if (!CanSendData()) return;

        // Must disable manage_cache to manually reset
        var json = JsonUtility.ToJson(new CacheResetPayload
        {
            manage_cache = false,
            reset_cache = true
        });

        Debug.Log($"[Scope] Resetting cache: {json}");
        dataChannel.Send(json);

        // Optionally restore manage_cache after reset
        if (manageCache)
        {
            StartCoroutine(RestoreManageCache());
        }
    }

    [Serializable]
    private class CacheResetPayload
    {
        public bool manage_cache;
        public bool reset_cache;
    }

    private IEnumerator RestoreManageCache()
    {
        yield return new WaitForSeconds(0.1f);
        if (CanSendData())
        {
            dataChannel.Send("{\"manage_cache\": true}");
        }
    }

    /// <summary>
    /// Toggle cache management mode.
    /// When disabled, you have manual control via ResetCache().
    /// </summary>
    public void SetManageCache(bool enabled)
    {
        if (!CanSendData()) return;

        manageCache = enabled;
        dataChannel.Send($"{{\"manage_cache\": {enabled.ToString().ToLower()}}}");
    }

    #endregion

    #region Public Methods - Parameters

    /// <summary>
    /// Update the generation prompt in real-time.
    /// </summary>
    public void UpdatePrompt(string newPrompt, float weight = 1.0f)
    {
        if (!CanSendData()) return;

        currentPrompt = newPrompt;

        var payload = new PromptUpdatePayload
        {
            prompts = new List<PromptData> { new PromptData { text = newPrompt, weight = weight } }
        };

        dataChannel.Send(JsonUtility.ToJson(payload));
    }

    [Serializable]
    private class PromptUpdatePayload
    {
        public List<PromptData> prompts;
    }

    /// <summary>
    /// Update denoising steps. More steps = higher quality, slower.
    /// </summary>
    public void UpdateDenoisingSteps(int[] steps)
    {
        if (!CanSendData()) return;

        // Must construct JSON manually for arrays
        string stepsJson = "[" + string.Join(",", steps) + "]";
        dataChannel.Send($"{{\"denoising_step_list\": {stepsJson}}}");
    }

    /// <summary>
    /// Update noise scale (video mode only).
    /// Higher = more variation, lower = more faithful to input.
    /// </summary>
    public void UpdateNoiseScale(float scale)
    {
        if (!CanSendData() || !useVideoMode) return;

        scale = Mathf.Clamp01(scale);
        dataChannel.Send($"{{\"noise_scale\": {scale}}}");
    }

    /// <summary>
    /// Pause or resume generation.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (!CanSendData()) return;

        dataChannel.Send($"{{\"paused\": {paused.ToString().ToLower()}}}");
    }

    #endregion

    #region Helper Methods

    private bool CanSendData()
    {
        if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("[Scope] Data channel not open");
            return false;
        }
        return true;
    }

    #endregion
}
```

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "Pipeline not loaded" error | Tried to connect before pipeline ready | Poll `/api/v1/pipeline/status` until `status == "loaded"` |
| No video received | ICE connection failed | Check TURN server credentials, firewall settings |
| Video looks frozen/corrupted | Cache got into bad state | Send `reset_cache: true` |
| `reset_cache` not working | `manage_cache` is enabled | First set `manage_cache: false`, then `reset_cache: true` |
| High latency | Resolution too high | Lower `width`/`height`, reduce denoising steps |
| Generation too slow | Too many denoising steps | Use fewer steps: `[750, 250]` instead of `[1000, 750, 500, 250]` |

### Debug Logging

Enable verbose logging on the server by checking the logs endpoint:
```http
GET /api/v1/logs/current
```

### WebRTC Connection States

Monitor these in your Unity console:
- `new` → Initial state
- `connecting` → Establishing connection
- `connected` → ✅ Ready to stream
- `disconnected` → Connection lost, may reconnect
- `failed` → ❌ Connection failed, need to restart

---

## Version Compatibility

This documentation is for:
- **Daydream Scope**: v0.1.0+
- **LongLive Pipeline**: Based on NVIDIA's LongLive model
- **Unity WebRTC**: com.unity.webrtc 3.0.0+

---

## Related Documentation

- [Server API Documentation](./server.md)
- [LoRA Configuration](./lora.md)
- [Spout Integration (Windows)](./spout.md)
- [LongLive Usage Guide](../src/scope/core/pipelines/longlive/docs/usage.md)
