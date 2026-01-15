# Video-to-Video Guide for All Scope Pipelines

This guide covers video-to-video (V2V) mode for all Scope pipelines that support it. Each pipeline has unique characteristics, default settings, and use cases.

## Pipelines with Video-to-Video Support

| Pipeline | ID | Base Model | VRAM | VACE Support | Default Mode |
|----------|-----|------------|------|--------------|--------------|
| LongLive | `longlive` | Wan2.1 1.3B | ~20GB | ✅ Yes | Text-to-Video |
| StreamDiffusionV2 | `streamdiffusionv2` | Wan2.1 1.3B | ~20GB | ✅ Yes | Video-to-Video |
| RewardForcing | `reward-forcing` | Wan2.1 1.3B | ~20GB | ✅ Yes | Text-to-Video |
| MemFlow | `memflow` | Wan2.1 1.3B | ~20GB | ✅ Yes | Text-to-Video |
| Krea Realtime Video | `krea-realtime-video` | Wan2.1 14B | ~32GB | ✅ Yes | Text-to-Video |

> **Note:** All pipelines use the same core WebRTC connection flow. The differences are in load parameters, default settings, and recommended configurations for V2V mode.

---

## Quick Start: Universal WebRTC Connection

The WebRTC connection process is identical for all pipelines. Only the `pipeline_id` and `initialParameters` differ.

```javascript
const API_BASE = "http://localhost:8000";

// Universal function that works for all pipelines
async function startV2VStream(pipelineId, inputStream, initialParams) {
  // 1. Get ICE servers
  const iceResponse = await fetch(`${API_BASE}/api/v1/webrtc/ice-servers`);
  const { iceServers } = await iceResponse.json();
  
  // 2. Create peer connection
  const pc = new RTCPeerConnection({ iceServers });
  
  let sessionId = null;
  const queuedCandidates = [];
  
  // 3. Create data channel
  const dataChannel = pc.createDataChannel("parameters", { ordered: true });
  
  dataChannel.onopen = () => console.log("Data channel ready");
  dataChannel.onmessage = (event) => {
    const data = JSON.parse(event.data);
    if (data.type === "stream_stopped") {
      console.log("Stream stopped:", data.error_message);
      pc.close();
    }
  };
  
  // 4. Add input video track
  inputStream.getTracks().forEach((track) => {
    if (track.kind === "video") {
      pc.addTrack(track, inputStream);
    }
  });
  
  // 5. Handle output video
  pc.ontrack = (event) => {
    if (event.streams[0]) {
      document.getElementById("outputVideo").srcObject = event.streams[0];
    }
  };
  
  // 6. ICE candidates
  pc.onicecandidate = async (event) => {
    if (event.candidate) {
      if (sessionId) {
        await sendIceCandidate(sessionId, event.candidate);
      } else {
        queuedCandidates.push(event.candidate);
      }
    }
  };
  
  // 7. Create offer
  const offer = await pc.createOffer();
  await pc.setLocalDescription(offer);
  
  const response = await fetch(`${API_BASE}/api/v1/webrtc/offer`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      sdp: pc.localDescription.sdp,
      type: pc.localDescription.type,
      initialParameters: {
        input_mode: "video",
        ...initialParams
      }
    })
  });
  
  const answer = await response.json();
  sessionId = answer.sessionId;
  
  await pc.setRemoteDescription({ type: answer.type, sdp: answer.sdp });
  
  for (const candidate of queuedCandidates) {
    await sendIceCandidate(sessionId, candidate);
  }
  
  return { pc, dataChannel, sessionId };
}

async function sendIceCandidate(sessionId, candidate) {
  await fetch(`${API_BASE}/api/v1/webrtc/offer/${sessionId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      candidates: [{
        candidate: candidate.candidate,
        sdpMid: candidate.sdpMid,
        sdpMLineIndex: candidate.sdpMLineIndex
      }]
    })
  });
}
```

---

# Pipeline-Specific Configurations

## 1. LongLive

**Best for:** General-purpose video transformation, smooth prompt transitions, long-form generation.

### Load Pipeline

```javascript
await fetch(`${API_BASE}/api/v1/pipeline/load`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    pipeline_id: "longlive",
    load_params: {
      height: 512,       // V2V default
      width: 512,        // V2V default
      seed: 42,
      vace_enabled: false,  // Set true for VACE mode
      vae_type: "wan"
    }
  })
});
```

### V2V Parameters

```javascript
const longliveParams = {
  prompts: [{ text: "A beautiful landscape", weight: 1.0 }],
  denoising_step_list: [1000, 750],  // V2V default (fewer steps than T2V)
  noise_scale: 0.7,                   // 0.0-1.0, lower = more input preservation
  noise_controller: true,             // Motion-aware noise adaptation
  vace_enabled: false                 // false for Normal mode
};

const { pc, dataChannel } = await startV2VStream(
  "longlive", 
  webcamStream, 
  longliveParams
);
```

### Key Characteristics
- Default V2V resolution: 512×512
- Supports cache management for smoother transitions
- Built-in LoRA for improved quality
- See [LongLive V2V Connection Guide](longlive-v2v-connection.md) for complete details

---

## 2. StreamDiffusionV2

**Best for:** Real-time video transformation, optimized for V2V workflows.

> **Note:** StreamDiffusionV2 defaults to Video-to-Video mode unlike other pipelines.

### Load Pipeline

```javascript
await fetch(`${API_BASE}/api/v1/pipeline/load`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    pipeline_id: "streamdiffusionv2",
    load_params: {
      height: 320,       // Default
      width: 576,        // Default
      seed: 42,
      vace_enabled: false,  // Set true for VACE mode
      vae_type: "wan"
    }
  })
});
```

### V2V Parameters

```javascript
const streamdiffusionParams = {
  prompts: [{ text: "Anime style portrait", weight: 1.0 }],
  denoising_step_list: [750, 250],  // Fewer steps = faster (default)
  noise_scale: 0.7,                  // Built-in default
  noise_controller: true,            // Built-in default
  vace_enabled: false
};

const { pc, dataChannel } = await startV2VStream(
  "streamdiffusionv2", 
  webcamStream, 
  streamdiffusionParams
);
```

### Key Characteristics
- **Optimized for V2V by default** - trained with video-to-video in mind
- Fewer denoising steps (750, 250) for faster generation
- Noise scale and controller enabled by default
- Lower latency than other pipelines for real-time applications
- VACE quality is noted to be "poor" compared to other pipelines (from [vace.md](../vace.md))

---

## 3. RewardForcing

**Best for:** High-quality generation with reward-based optimization.

### Load Pipeline

```javascript
await fetch(`${API_BASE}/api/v1/pipeline/load`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    pipeline_id: "reward-forcing",
    load_params: {
      height: 512,       // V2V default
      width: 512,        // V2V default
      seed: 42,
      vace_enabled: false,
      vae_type: "wan"
    }
  })
});
```

### V2V Parameters

```javascript
const rewardForcingParams = {
  prompts: [{ text: "Cinematic scene, film grain", weight: 1.0 }],
  denoising_step_list: [1000, 750],  // V2V default
  noise_scale: 0.7,
  noise_controller: true,
  vace_enabled: false
};

const { pc, dataChannel } = await startV2VStream(
  "reward-forcing", 
  webcamStream, 
  rewardForcingParams
);
```

### Key Characteristics
- Trained with Rewarded Distribution Matching Distillation
- Higher quality output at the cost of some speed
- V2V resolution: 512×512
- Full VACE support

---

## 4. MemFlow

**Best for:** Long-form video generation with improved temporal consistency.

### Load Pipeline

```javascript
await fetch(`${API_BASE}/api/v1/pipeline/load`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    pipeline_id: "memflow",
    load_params: {
      height: 512,       // V2V default
      width: 512,        // V2V default
      seed: 42,
      vace_enabled: false,
      vae_type: "wan"
    }
  })
});
```

### V2V Parameters

```javascript
const memflowParams = {
  prompts: [{ text: "A person walking through a forest", weight: 1.0 }],
  denoising_step_list: [1000, 750],  // V2V default
  noise_scale: 0.7,
  noise_controller: true,
  vace_enabled: false
};

const { pc, dataChannel } = await startV2VStream(
  "memflow", 
  webcamStream, 
  memflowParams
);
```

### Key Characteristics
- Memory bank for improved long context consistency
- Based on LongLive architecture with memory enhancements
- Better at maintaining character/scene consistency over time
- Full VACE support

---

## 5. Krea Realtime Video

**Best for:** Highest quality generation with the 14B model.

> **Important:** This pipeline requires ~32GB VRAM (more with VACE). FP8 quantization is enabled by default.

### Load Pipeline

```javascript
await fetch(`${API_BASE}/api/v1/pipeline/load`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    pipeline_id: "krea-realtime-video",
    load_params: {
      height: 256,       // V2V default (smaller for performance)
      width: 256,        // V2V default
      seed: 42,
      quantization: "fp8_e4m3fn",  // Enabled by default
      vace_enabled: false,
      vae_type: "wan"
    }
  })
});
```

### V2V Parameters

```javascript
const kreaParams = {
  prompts: [{ text: "Photorealistic portrait, 4K detail", weight: 1.0 }],
  denoising_step_list: [1000, 750],  // V2V default
  noise_scale: 0.7,
  noise_controller: true,
  kv_cache_attention_bias: 0.3,  // Unique to Krea - helps prevent repetitive motion
  vace_enabled: false
};

const { pc, dataChannel } = await startV2VStream(
  "krea-realtime-video", 
  webcamStream, 
  kreaParams
);
```

### Key Characteristics
- Based on Wan2.1 14B (highest quality)
- V2V resolution: 256×256 by default (for performance)
- Supports `kv_cache_attention_bias` for motion control
- FP8 quantization enabled by default
- VACE requires ~55GB VRAM (from [vace.md](../vace.md))
- **Note:** VACE with Krea has limited functionality for continued prompting due to cache recomputation

---

# Comparison: V2V Defaults by Pipeline

| Pipeline | V2V Resolution | Denoising Steps | Noise Scale | Noise Controller |
|----------|----------------|-----------------|-------------|------------------|
| LongLive | 512×512 | [1000, 750] | 0.7 | ✅ Yes |
| StreamDiffusionV2 | 320×576 | [750, 250] | 0.7 | ✅ Yes |
| RewardForcing | 512×512 | [1000, 750] | 0.7 | ✅ Yes |
| MemFlow | 512×512 | [1000, 750] | 0.7 | ✅ Yes |
| Krea Realtime | 256×256 | [1000, 750] | 0.7 | ✅ Yes |

---

# Common V2V Parameters

These parameters work across all pipelines:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `input_mode` | string | - | Must be `"video"` for V2V |
| `prompts` | array | - | Text prompts with weights |
| `denoising_step_list` | array | varies | Noise levels for denoising |
| `noise_scale` | float | 0.7 | Input preservation (0=keep all, 1=ignore) |
| `noise_controller` | bool | true | Motion-aware noise adaptation |

## VACE Parameters (When Enabled)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `vace_enabled` | bool | true | Enable VACE mode |
| `vace_use_input_video` | bool | true | Route video to VACE conditioning |
| `vace_ref_images` | array | [] | Reference image paths |
| `vace_context_scale` | float | 1.0 | Reference influence (0.0-2.0) |

---

# Updating Parameters at Runtime

All pipelines support real-time parameter updates via the data channel:

```javascript
// Update prompt
dataChannel.send(JSON.stringify({
  prompts: [{ text: "New prompt", weight: 1.0 }]
}));

// Update noise scale
dataChannel.send(JSON.stringify({
  noise_scale: 0.5  // Less noise = more input preservation
}));

// Update denoising steps
dataChannel.send(JSON.stringify({
  denoising_step_list: [800, 400]
}));

// Update VACE parameters (if VACE enabled)
dataChannel.send(JSON.stringify({
  vace_context_scale: 1.5,
  vace_ref_images: ["/path/to/image.png"]
}));

// Krea-specific: Update KV cache bias
dataChannel.send(JSON.stringify({
  kv_cache_attention_bias: 0.5
}));
```

---

# Uploading Reference Images

Reference images for VACE work the same way across all pipelines:

```javascript
async function uploadReferenceImage(file) {
  const arrayBuffer = await file.arrayBuffer();
  const filename = encodeURIComponent(file.name);
  
  const response = await fetch(
    `${API_BASE}/api/v1/assets?filename=${filename}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/octet-stream" },
      body: arrayBuffer
    }
  );
  
  const result = await response.json();
  return result.path;  // Use this in vace_ref_images
}
```

---

# Stopping the Stream

```javascript
function stopStream(pc, dataChannel) {
  if (dataChannel) dataChannel.close();
  if (pc) pc.close();
}
```

---

# Pipeline Selection Guide

```
Need highest quality and have 32GB+ VRAM?
  → Use Krea Realtime Video

Need real-time performance with V2V optimization?
  → Use StreamDiffusionV2

Need smooth prompt transitions and general-purpose V2V?
  → Use LongLive

Need long-form consistency with memory?
  → Use MemFlow

Need reward-optimized quality?
  → Use RewardForcing
```

---

# Troubleshooting

## All Pipelines

- **Connection fails**: Ensure server is running (`uv run daydream-scope`)
- **No video output**: Check `input_mode: "video"` is set
- **Poor quality**: Increase resolution, add more denoising steps
- **Too slow**: Reduce resolution, use fewer denoising steps

## Pipeline-Specific

### Krea Realtime Video
- Requires ~32GB VRAM minimum
- VACE requires ~55GB VRAM
- FP8 quantization is recommended

### StreamDiffusionV2
- VACE quality is lower than other pipelines
- Optimized for speed over quality in V2V

### MemFlow / LongLive
- Best all-around balance for V2V
- Full VACE support with good quality

---

# Related Documentation

- [LongLive V2V Connection Guide](longlive-v2v-connection.md) - Detailed guide for LongLive
- [VACE Usage Guide](vace.md) - Complete VACE documentation
- [Load Pipeline API](load.md) - Pipeline loading reference
- [Send and Receive Video](sendreceive.md) - WebRTC basics
