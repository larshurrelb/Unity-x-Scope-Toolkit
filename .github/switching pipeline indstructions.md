# Daydream Scope - API Pipeline Switching & Pipeline Comparison Guide

## Table of Contents

1. [Switching Pipelines via API (from Unity)](#1-switching-pipelines-via-api-from-unity)
2. [Complete API Reference for External Clients](#2-complete-api-reference-for-external-clients)
3. [Pipeline Comparison: LongLive vs StreamDiffusionV2 vs KREA Realtime Video](#3-pipeline-comparison)
4. [Why KREA Might Fail When LongLive / StreamDiffusionV2 Work](#4-why-krea-might-fail)
5. [Quick Reference: cURL Examples](#5-quick-reference-curl-examples)

---

## 1. Switching Pipelines via API (from Unity)

You can switch pipelines entirely through HTTP calls without touching the Scope UI. The flow is:

```
1. POST /api/v1/pipeline/load     →  Tell server to load a pipeline
2. GET  /api/v1/pipeline/status   →  Poll until status == "loaded"
3. POST /api/v1/webrtc/offer      →  Establish WebRTC connection
4. WebRTC Data Channel            →  Send runtime parameter updates
```

### Step 1: Load a Pipeline

```http
POST http://<RUNPOD_HOST>:8000/api/v1/pipeline/load
Content-Type: application/json

{
  "pipeline_ids": ["longlive"],
  "load_params": {
    "height": 320,
    "width": 576,
    "base_seed": 42,
    "vace_enabled": true,
    "vae_type": "wan"
  }
}
```

Response (immediate, loading happens async):
```json
{ "message": "Pipeline loading initiated successfully" }
```

**To switch pipelines mid-session**, just POST again with a different `pipeline_ids`. The server automatically unloads the current pipeline and loads the new one. Your WebRTC connection will keep running but frames will halt until the new pipeline finishes loading.

### Step 2: Poll for Load Completion

```http
GET http://<RUNPOD_HOST>:8000/api/v1/pipeline/status
```

Response while loading:
```json
{
  "status": "loading",
  "pipeline_id": "longlive",
  "load_params": null,
  "loaded_lora_adapters": null,
  "error": null
}
```

Response when ready:
```json
{
  "status": "loaded",
  "pipeline_id": "longlive",
  "load_params": {
    "height": 320,
    "width": 576,
    "base_seed": 42,
    "vace_enabled": true,
    "vae_type": "wan"
  },
  "loaded_lora_adapters": null,
  "error": null
}
```

Possible `status` values: `"not_loaded"`, `"loading"`, `"loaded"`, `"error"`

### Step 3: Establish WebRTC

The pipeline **must** be `"loaded"` before you can connect via WebRTC, otherwise the server returns HTTP 400.

```http
POST http://<RUNPOD_HOST>:8000/api/v1/webrtc/offer
Content-Type: application/json

{
  "sdp": "<your SDP offer string>",
  "type": "offer",
  "initialParameters": {
    "input_mode": "video",
    "prompts": [
      { "text": "a cyberpunk cityscape", "weight": 1.0 }
    ],
    "denoising_step_list": [1000, 750, 500, 250],
    "manage_cache": true
  }
}
```

Response:
```json
{
  "sdp": "<SDP answer>",
  "type": "answer",
  "sessionId": "abc-123-..."
}
```

**ICE Candidates** (Trickle ICE):
```http
PATCH http://<RUNPOD_HOST>:8000/api/v1/webrtc/offer/{sessionId}
Content-Type: application/json

{
  "candidate": "<ICE candidate string>",
  "sdpMid": "0",
  "sdpMLineIndex": 0
}
```

**ICE Servers** (for NAT traversal, especially on RunPod):
```http
GET http://<RUNPOD_HOST>:8000/api/v1/webrtc/ice-servers
```

### Step 4: Runtime Parameter Updates via Data Channel

After WebRTC is connected, create a data channel named `"parameters"` with `ordered: true`. Send JSON messages to update parameters in real time:

```json
{
  "prompts": [
    { "text": "an oil painting of mountains", "weight": 1.0 }
  ],
  "noise_scale": 0.5,
  "denoising_step_list": [1000, 750],
  "vace_context_scale": 1.2
}
```

You can also pause/resume:
```json
{ "paused": true }
```

### Pipeline Switching Without Reconnecting WebRTC

You can switch pipelines while a WebRTC session exists:

1. POST `/api/v1/pipeline/load` with the new pipeline
2. Poll `/api/v1/pipeline/status` until `"loaded"`
3. Frames will resume automatically on the existing WebRTC connection

However, it's generally cleaner to:
1. Close the existing WebRTC connection
2. Load the new pipeline
3. Establish a fresh WebRTC connection

---

## 2. Complete API Reference for External Clients

### Pipeline Discovery

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/pipelines/schemas` | GET | List all available pipelines with their config schemas, modes, defaults |

### Pipeline Lifecycle

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/pipeline/load` | POST | Load pipeline(s). Body: `{ pipeline_ids: string[], load_params?: object }` |
| `/api/v1/pipeline/status` | GET | Get current pipeline status |

### WebRTC Streaming

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/webrtc/ice-servers` | GET | Get STUN/TURN server configuration |
| `/api/v1/webrtc/offer` | POST | Send SDP offer, get SDP answer + sessionId |
| `/api/v1/webrtc/offer/{sessionId}` | PATCH | Add ICE candidate (Trickle ICE) |

### Assets (for VACE reference images)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/assets` | POST | Upload asset. Query: `?filename=<name>`. Body: raw file data |
| `/api/v1/assets` | GET | List assets. Query: `?type=image` |

### Runtime Parameters (via WebRTC Data Channel)

| Parameter | Type | Description |
|-----------|------|-------------|
| `input_mode` | `"text"` \| `"video"` | Switch between text-to-video and video-to-video |
| `prompts` | `[{text, weight}]` | Text prompts for generation |
| `prompt_interpolation_method` | `"linear"` \| `"slerp"` | Blending method between prompts |
| `denoising_step_list` | `int[]` | Timesteps, descending (e.g. `[1000, 750, 500, 250]`) |
| `noise_scale` | `0.0-1.0` | Noise injection strength |
| `noise_controller` | `bool` | Auto-adjust noise based on motion |
| `manage_cache` | `bool` | Auto KV cache management |
| `reset_cache` | `bool` | One-shot cache reset |
| `kv_cache_attention_bias` | `0.01-1.0` | How much to rely on past frames |
| `vace_enabled` | `bool` | Toggle VACE |
| `vace_ref_images` | `string[]` | Reference image paths |
| `vace_context_scale` | `0.0-2.0` | VACE conditioning strength |
| `paused` | `bool` | Pause/resume generation |
| `recording` | `bool` | Start/stop recording |
| `lora_scales` | `[{path, scale}]` | Update LoRA weights at runtime (requires `runtime_peft` mode) |

---

## 3. Pipeline Comparison

### Summary Table

| Feature | LongLive | StreamDiffusionV2 | KREA Realtime Video |
|---------|----------|-------------------|---------------------|
| **Base Model** | Wan 1.3B | Wan 1.3B | **Wan 14B** |
| **VRAM Required** | 20 GB | 20 GB | **32 GB (40 GB+ recommended)** |
| **VACE Module** | 1.3B variant | 1.3B variant | **14B variant** |
| **Default Quantization** | None | None | **FP8_E4M3FN** |
| **Default Mode** | text | video | text |
| **Default Resolution** | 320x576 | 512x512 | 320x576 (text), 256x256 (video) |
| **Denoising Steps (text)** | [1000, 750, 500, 250] | [1000, 750] | [1000, 750, 500, 250] |
| **Denoising Steps (video)** | [1000, 750] | [750, 250] | [1000, 750] |
| **num_frame_per_block** | 3 | 1 | 3 |
| **local_attn_size** | 12 | 6 | 6 |
| **global_sink** | Yes | implicit | No |
| **KV Cache Attention Bias** | 1.0 | 1.0 | **0.3** |
| **supports_kv_cache_bias** | No | No | **Yes** |
| **Built-in LoRA** | Yes (performance LoRA) | No | No |
| **Projection Fusion** | No | No | **Yes** |
| **torch.compile** | No | No | **Yes (H100/Hopper only)** |
| **Warmup Required** | No | No | **Yes (3 iterations)** |
| **Custom VAE Wrapper** | No | Yes (skips latent norm) | No |

### Load Parameters Side-by-Side

#### LongLive (`"longlive"`)

```json
{
  "pipeline_ids": ["longlive"],
  "load_params": {
    "height": 320,
    "width": 576,
    "base_seed": 42,
    "quantization": null,
    "vace_enabled": true,
    "vae_type": "wan",
    "loras": null,
    "lora_merge_mode": "permanent_merge"
  }
}
```

#### StreamDiffusionV2 (`"streamdiffusionv2"`)

```json
{
  "pipeline_ids": ["streamdiffusionv2"],
  "load_params": {
    "height": 512,
    "width": 512,
    "base_seed": 42,
    "quantization": null,
    "vace_enabled": true,
    "vae_type": "wan",
    "loras": null,
    "lora_merge_mode": "permanent_merge"
  }
}
```

#### KREA Realtime Video (`"krea-realtime-video"`)

```json
{
  "pipeline_ids": ["krea-realtime-video"],
  "load_params": {
    "height": 320,
    "width": 576,
    "base_seed": 42,
    "quantization": "fp8_e4m3fn",
    "vace_enabled": true,
    "vae_type": "wan",
    "loras": null,
    "lora_merge_mode": "permanent_merge"
  }
}
```

**Key differences in KREA load params:**
- `quantization` defaults to `"fp8_e4m3fn"` (the others default to `null` / no quantization)
- Same height/width defaults as LongLive (320x576), but video mode drops to 256x256

### Initialization Differences

**LongLive:**
1. Load CausalWanModel (1.3B)
2. Apply VACE wrapper
3. Apply built-in performance LoRA
4. Apply user LoRAs
5. Quantize (if requested)
6. Load text encoder + VAE
7. Ready immediately

**StreamDiffusionV2:**
1. Load CausalWanModel (1.3B)
2. Apply VACE wrapper
3. Apply user LoRAs (no built-in LoRA)
4. Quantize (if requested)
5. Load text encoder + custom VAE wrapper
6. Ready immediately

**KREA Realtime Video:**
1. Load CausalWanModel (**14B**)
2. **Fuse attention projections on all blocks**
3. **Load text encoder BEFORE VACE** (different order)
4. Apply VACE wrapper (14B variant, with quantization param)
5. Apply user LoRAs
6. Quantize (if requested)
7. Load VAE
8. **MANDATORY WARMUP: 3 generation iterations to fill KV cache**
9. **Optional torch.compile on H100/Hopper GPUs**
10. Ready after warmup completes

---

## 4. Why KREA Might Fail When LongLive / StreamDiffusionV2 Work

### Most Likely Causes (check in this order)

#### 1. Insufficient VRAM (32 GB minimum)
KREA uses the 14B model (10x larger than the 1.3B used by the other two). It requires at minimum 32 GB VRAM, with 40 GB+ recommended.

- **Symptom:** OOM errors during loading or warmup
- **Fix:** Ensure your RunPod instance has an A100 40GB/80GB or H100. An RTX 3090/4090 (24 GB) is **not enough** for KREA
- **Workaround:** KREA defaults to `fp8_e4m3fn` quantization to reduce memory. If you're passing `quantization: null`, change it to `"fp8_e4m3fn"`

#### 2. Model Files Not Downloaded
KREA requires different model files than LongLive/StreamDiffusionV2:

| Model File | Used By |
|------------|---------|
| `Wan2.1-T2V-1.3B/` | LongLive, StreamDiffusionV2 |
| `Wan2.1-T2V-14B/config.json` | **KREA only** |
| `krea-realtime-video/krea-realtime-video-14b.safetensors` | **KREA only** |
| `WanVideo_comfy/Wan2_1-VACE_module_14B_bf16.safetensors` | **KREA only** (14B VACE) |
| `WanVideo_comfy/Wan2_1-VACE_module_bf16.safetensors` | LongLive, StreamDiffusionV2 (1.3B VACE) |

- **Symptom:** FileNotFoundError or download hangs during loading
- **Fix:** Let models auto-download (first load will be slow), or pre-download to `~/.daydream-scope/models` (or `/workspace/models` on RunPod)

#### 3. Warmup Failure
KREA runs 3 warmup generation iterations during initialization to pre-fill the KV cache and compile flex_attention kernels. If any warmup iteration fails, the pipeline won't load.

- **Symptom:** Pipeline status stuck on `"loading"` or goes to `"error"` after a long time
- **Fix:** Check server logs for errors during warmup. Common issues:
  - OOM during warmup (VRAM)
  - PyTorch version too old for flex_attention (needs PyTorch 2.4+)

#### 4. PyTorch / flex_attention Compatibility
KREA uses `torch.nn.attention.flex_attention` which requires PyTorch 2.4+.

- **Symptom:** `ImportError` or `RuntimeError` related to flex_attention
- **Fix:** Ensure your environment has PyTorch >= 2.4 with CUDA support

#### 5. torch.compile Issues (H100/Hopper only)
On H100/Hopper GPUs, KREA automatically enables `torch.compile` on attention blocks. This can cause issues with certain CUDA/PyTorch versions.

- **Symptom:** Errors mentioning `torch._dynamo`, `triton`, or compilation failures
- **Fix:** This is hard to work around without code changes. Check that your CUDA toolkit version is compatible with the PyTorch version

#### 6. Different KV Cache Attention Bias Default
KREA defaults to `kv_cache_attention_bias: 0.3` instead of the `1.0` used by the other pipelines. This shouldn't cause failures but could cause unexpected visual results.

- **Note:** KREA is the only pipeline that exposes `supports_kv_cache_bias: True`, meaning this parameter can be adjusted at runtime

### Debugging Checklist

```
[ ] RunPod GPU has >= 32 GB VRAM (A100/H100)
[ ] Pipeline status shows "error" → check error message
[ ] Server logs for OOM, ImportError, or FileNotFoundError
[ ] All KREA model files downloaded in models directory
[ ] PyTorch version >= 2.4
[ ] Try with explicit quantization: "fp8_e4m3fn"
[ ] Try with vace_enabled: false (reduces VRAM usage)
[ ] Try with smaller resolution: height=256, width=256
```

---

## 5. Quick Reference: cURL Examples

### List Available Pipelines
```bash
curl http://<HOST>:8000/api/v1/pipelines/schemas | python3 -m json.tool
```

### Load LongLive
```bash
curl -X POST http://<HOST>:8000/api/v1/pipeline/load \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_ids": ["longlive"],
    "load_params": {
      "height": 320,
      "width": 576,
      "vace_enabled": true
    }
  }'
```

### Load StreamDiffusionV2
```bash
curl -X POST http://<HOST>:8000/api/v1/pipeline/load \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_ids": ["streamdiffusionv2"],
    "load_params": {
      "height": 512,
      "width": 512,
      "vace_enabled": true
    }
  }'
```

### Load KREA Realtime Video
```bash
curl -X POST http://<HOST>:8000/api/v1/pipeline/load \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_ids": ["krea-realtime-video"],
    "load_params": {
      "height": 320,
      "width": 576,
      "quantization": "fp8_e4m3fn",
      "vace_enabled": true
    }
  }'
```

### Check Pipeline Status
```bash
curl http://<HOST>:8000/api/v1/pipeline/status | python3 -m json.tool
```

### Switch Pipeline (just load a different one)
```bash
# Currently running longlive, switch to streamdiffusionv2:
curl -X POST http://<HOST>:8000/api/v1/pipeline/load \
  -H "Content-Type: application/json" \
  -d '{"pipeline_ids": ["streamdiffusionv2"]}'
```

### Poll Until Loaded (bash loop)
```bash
while true; do
  STATUS=$(curl -s http://<HOST>:8000/api/v1/pipeline/status | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
  echo "Status: $STATUS"
  if [ "$STATUS" = "loaded" ]; then break; fi
  if [ "$STATUS" = "error" ]; then echo "FAILED"; break; fi
  sleep 2
done
```

### Pipeline Chaining (preprocessor + main pipeline)
```bash
curl -X POST http://<HOST>:8000/api/v1/pipeline/load \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_ids": ["video-depth-anything", "longlive"],
    "load_params": {
      "height": 320,
      "width": 576,
      "vace_enabled": true
    }
  }'
```

---

## Unity Integration Notes

For your Unity WebRTC implementation:

1. **Pipeline switching** is just an HTTP POST — no WebRTC involved. You can use Unity's `UnityWebRequest` to call `/api/v1/pipeline/load` and `/api/v1/pipeline/status`

2. **After switching**, you may need to re-establish the WebRTC peer connection since the old pipeline gets unloaded and the output format/resolution may change

3. **Parameter updates** go through the WebRTC data channel (not HTTP). Create a data channel named `"parameters"` with `ordered: true`

4. **ICE servers** — if you're connecting to RunPod, make sure to fetch ICE servers from `/api/v1/webrtc/ice-servers` and set `HF_TOKEN` on the server for TURN server access through firewalls

5. **Available pipeline IDs** for the load request:
   - `"longlive"` — 1.3B model, 20GB VRAM, works on RTX 3090/4090
   - `"streamdiffusionv2"` — 1.3B model, 20GB VRAM, optimized for video-to-video
   - `"krea-realtime-video"` — 14B model, 32GB+ VRAM, needs A100/H100
   - `"reward-forcing"` — reward-guided generation
   - `"memflow"` — memory-efficient flow-based
   - `"passthrough"` — pass-through (useful for testing)
   - Preprocessors: `"video-depth-anything"`, `"scribble"`, `"gray"`, `"optical-flow"`, `"controller-viz"`
   - Postprocessors: `"rife"` (frame interpolation)
