# LongLive Video-to-Video Connection Guide

This guide explains how to initialize a WebRTC connection to the Scope LongLive pipeline for video-to-video mode, covering both **Normal Mode** (latent initialization) and **VACE Mode** (conditioning-based guidance).

## Overview

LongLive supports two distinct video-to-video approaches:

### Normal Mode (VACE OFF)
When VACE is disabled (`vace_enabled: false`) or `vace_use_input_video: false`:
- Input video frames are encoded to latents via VAE
- Noise is added to these latents (controlled by `noise_scale`)
- The noisy latents serve as the starting point for diffusion denoising
- The output video follows the structure of your input while being transformed by prompts

### VACE Mode (VACE ON)
When VACE is enabled (`vace_enabled: true`) and `vace_use_input_video: true`:
- Input video is encoded as VACE conditioning context
- Latents start as pure noise
- The input video guides generation through "hint injection" in transformer blocks
- Can be combined with reference images for style + structural control

## VACE vs Normal Mode: When to Use Which?

### Advantages of VACE Mode

| Benefit | Description |
|---------|-------------|
| **Structural Guidance** | VACE treats input as a "control signal" (like depth maps, pose, or optical flow), providing architectural guidance without constraining the latent space |
| **Reference Image Support** | Can combine input video with reference images for style/character consistency |
| **Cleaner Transformations** | Since latents start fresh, outputs aren't "polluted" by input artifacts |
| **Better for Control Videos** | Ideal when input is preprocessed (depth, pose, scribble) rather than raw video |
| **Animate Anything** | Combine a reference image (character/style) with a control video (motion/structure) |

### Advantages of Normal Mode

| Benefit | Description |
|---------|-------------|
| **Skips VACE Encoding** | No additional VACE encoding step, reducing computational overhead |
| **Direct Influence** | Input video directly shapes the latent space, giving tighter control |
| **Fine-Grained Control** | `noise_scale` parameter allows precise blending between input preservation and creative freedom |
| **Lower VRAM Requirement** | Normal mode doesn't require the ~48GB VRAM that VACE needs (see [mixin.py](../../src/scope/core/pipelines/wan2_1/vace/mixin.py#L14-L16)) |
| **Simpler Mental Model** | Input → Add Noise → Denoise with prompt = Output |

### Decision Guide

```
Do you want to use preprocessed control videos (depth, pose, flow)?
  → YES: Use VACE Mode

Do you want to combine reference images with video input?
  → YES: Use VACE Mode

Do you need maximum speed for real-time applications?
  → YES: Use Normal Mode

Do you want fine control over how much input is preserved vs transformed?
  → YES: Use Normal Mode (adjust noise_scale)

Are you doing style transfer on raw webcam/screen capture?
  → Either works, but Normal Mode is faster
```

## Prerequisites

1. Scope server is running: `uv run daydream-scope`
2. LongLive models are downloaded
3. Pipeline is loaded (with or without VACE)

---

# Part 1: Normal Mode (VACE OFF)

This section covers video-to-video with latent initialization (faster, simpler).

## Step 1: Load the Pipeline (Normal Mode)

Load the LongLive pipeline with VACE disabled:

```javascript
const API_BASE = "http://localhost:8000";

async function loadPipeline() {
  const response = await fetch(`${API_BASE}/api/v1/pipeline/load`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      pipeline_id: "longlive",
      load_params: {
        height: 320,
        width: 576,
        seed: 42,
        vace_enabled: false  // Disable VACE for latent initialization mode
      }
    })
  });

  if (!response.ok) {
    throw new Error(`Failed to load pipeline: ${await response.text()}`);
  }

  return await response.json();
}

async function waitForPipelineReady(timeoutMs = 300000) {
  const startTime = Date.now();

  while (Date.now() - startTime < timeoutMs) {
    const response = await fetch(`${API_BASE}/api/v1/pipeline/status`);
    const status = await response.json();

    if (status.status === "loaded") {
      console.log("Pipeline ready:", status.pipeline_id);
      return status;
    } else if (status.status === "error") {
      throw new Error(`Pipeline load failed: ${status.error}`);
    }

    // Still loading, wait and retry
    await new Promise(r => setTimeout(r, 1000));
  }

  throw new Error("Pipeline load timeout");
}

// Load and wait for pipeline
await loadPipeline();
await waitForPipelineReady();
```

## Step 2: Get Video Input Source

Obtain a video stream from webcam, screen capture, or file (applies to both modes):

```javascript
// Option A: Webcam
async function getWebcamStream() {
  return await navigator.mediaDevices.getUserMedia({
    video: {
      width: { ideal: 576 },
      height: { ideal: 320 },
      frameRate: { ideal: 30 }
    }
  });
}

// Option B: Screen capture
async function getScreenStream() {
  return await navigator.mediaDevices.getDisplayMedia({
    video: {
      width: { ideal: 576 },
      height: { ideal: 320 },
      frameRate: { ideal: 30 }
    }
  });
}

// Option C: Video file via canvas
function getVideoFileStream(videoElement) {
  const canvas = document.createElement("canvas");
  canvas.width = 576;
  canvas.height = 320;
  const ctx = canvas.getContext("2d");

  // Draw video frames to canvas
  function drawFrame() {
    if (!videoElement.paused && !videoElement.ended) {
      ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
      requestAnimationFrame(drawFrame);
    }
  }

  videoElement.play();
  drawFrame();

  return canvas.captureStream(30);
}
```

## Step 3: Initialize WebRTC Connection (Normal Mode)

Set up the WebRTC peer connection with video input for Normal Mode:

```javascript
async function startVideoToVideoStream(inputStream, initialPrompt = "A painting") {
  // 1. Get ICE servers from backend
  const iceResponse = await fetch(`${API_BASE}/api/v1/webrtc/ice-servers`);
  const { iceServers } = await iceResponse.json();

  // 2. Create peer connection
  const pc = new RTCPeerConnection({ iceServers });

  // Session state
  let sessionId = null;
  const queuedCandidates = [];

  // 3. Create data channel for parameter updates
  const dataChannel = pc.createDataChannel("parameters", { ordered: true });

  dataChannel.onopen = () => {
    console.log("Data channel opened - ready for parameter updates");
  };

  dataChannel.onmessage = (event) => {
    const data = JSON.parse(event.data);
    if (data.type === "stream_stopped") {
      console.log("Stream stopped:", data.error_message);
      pc.close();
    }
  };

  dataChannel.onerror = (error) => {
    console.error("Data channel error:", error);
  };

  // 4. Add input video track to peer connection
  inputStream.getTracks().forEach((track) => {
    if (track.kind === "video") {
      console.log("Adding video track for sending to server");
      pc.addTrack(track, inputStream);
    }
  });

  // 5. Handle incoming video track (generated output)
  pc.ontrack = (event) => {
    if (event.streams && event.streams[0]) {
      const outputVideo = document.getElementById("outputVideo");
      outputVideo.srcObject = event.streams[0];
      console.log("Receiving generated video from server");
    }
  };

  // 6. Monitor connection state
  pc.onconnectionstatechange = () => {
    console.log("Connection state:", pc.connectionState);
    if (pc.connectionState === "connected") {
      console.log("WebRTC connected - streaming active");
    } else if (pc.connectionState === "failed" || pc.connectionState === "disconnected") {
      console.log("WebRTC disconnected");
    }
  };

  // 7. Handle ICE candidates (Trickle ICE)
  pc.onicecandidate = async (event) => {
    if (event.candidate) {
      if (sessionId) {
        await sendIceCandidate(sessionId, event.candidate);
      } else {
        // Queue candidates until we have session ID
        queuedCandidates.push(event.candidate);
      }
    }
  };

  // 8. Create and send offer with initial parameters
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
        prompts: [{ text: initialPrompt, weight: 1.0 }],
        denoising_step_list: [700, 500],
        noise_scale: 0.7,  // Controls how much noise vs input video (0.0-1.0)
        vace_enabled: false  // Ensure VACE is disabled
      }
    })
  });

  if (!response.ok) {
    throw new Error(`WebRTC offer failed: ${await response.text()}`);
  }

  const answer = await response.json();
  sessionId = answer.sessionId;

  // 9. Set remote description
  await pc.setRemoteDescription({
    type: answer.type,
    sdp: answer.sdp
  });

  // 10. Send queued ICE candidates
  for (const candidate of queuedCandidates) {
    await sendIceCandidate(sessionId, candidate);
  }
  queuedCandidates.length = 0;

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

## Step 4: Update Parameters During Streaming

Use the data channel to update parameters in real-time:

```javascript
function updatePrompt(dataChannel, newPrompt) {
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      prompts: [{ text: newPrompt, weight: 1.0 }]
    }));
  }
}

function updateNoiseScale(dataChannel, noiseScale) {
  // Higher = more noise, less input influence (0.0-1.0)
  // Lower = less noise, more input influence
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      noise_scale: noiseScale
    }));
  }
}

function updateDenoisingSteps(dataChannel, steps) {
  // Controls quality vs speed tradeoff
  // More steps = higher quality, slower
  // Fewer steps = lower quality, faster
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      denoising_step_list: steps  // e.g., [700, 500] or [800, 600, 400]
    }));
  }
}
```

## Step 5: Stop the Stream

Clean up the connection when done:

```javascript
function stopStream(pc, dataChannel, sessionId) {
  // Close data channel
  if (dataChannel) {
    dataChannel.close();
  }

  // Close peer connection
  if (pc) {
    pc.close();
  }

  console.log(`Stream stopped for session ${sessionId}`);
}
```

## Complete Example

```javascript
async function main() {
  const API_BASE = "http://localhost:8000";

  // 1. Load pipeline with VACE disabled
  console.log("Loading LongLive pipeline...");
  await loadPipeline();
  await waitForPipelineReady();

  // 2. Get webcam stream
  console.log("Getting webcam stream...");
  const inputStream = await getWebcamStream();

  // 3. Start video-to-video streaming
  console.log("Starting video-to-video stream...");
  const { pc, dataChannel, sessionId } = await startVideoToVideoStream(
    inputStream,
    "A beautiful oil painting, vibrant colors"
  );

  // 4. Update prompt after 5 seconds
  setTimeout(() => {
    updatePrompt(dataChannel, "A cyberpunk scene, neon lights");
  }, 5000);

  // 5. Stop after 30 seconds
  setTimeout(() => {
    stopStream(pc, dataChannel, sessionId);
    inputStream.getTracks().forEach(track => track.stop());
  }, 30000);
}

main().catch(console.error);
```

## Key Parameters (Normal Mode)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `input_mode` | string | - | Set to `"video"` for V2V mode |
| `prompts` | array | - | Text prompts with weights |
| `noise_scale` | float | 0.7 | Amount of noise added to input (0.0-1.0). Lower = more input influence |
| `denoising_step_list` | array | [700, 500] | Noise levels for denoising steps |
| `vace_enabled` | bool | true | Set to `false` for latent initialization mode |

## How Normal Mode Works

When VACE is disabled, the video-to-video pipeline works as follows:

1. **Input Video Encoding**: Your input video frames are encoded to latent space using the VAE
2. **Noise Addition**: Gaussian noise is added to the latents, controlled by `noise_scale`:
   - `noise_scale = 1.0`: Pure noise (ignores input video)
   - `noise_scale = 0.7`: 70% noise, 30% input video latents (default)
   - `noise_scale = 0.0`: No noise (exact input reconstruction)
3. **Denoising**: The noisy latents are denoised step-by-step guided by your text prompts
4. **Decoding**: Final latents are decoded back to video pixels

This creates a transformation effect where the input video's structure and motion influence the output while the text prompt controls the style and content.

---

# Part 2: VACE Mode

This section covers video-to-video with VACE conditioning (more powerful, supports reference images).

## Step 1: Load the Pipeline (VACE Mode)

Load the LongLive pipeline with VACE enabled:

```javascript
const API_BASE = "http://localhost:8000";

async function loadPipelineWithVACE() {
  const response = await fetch(`${API_BASE}/api/v1/pipeline/load`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      pipeline_id: "longlive",
      load_params: {
        height: 320,
        width: 576,
        seed: 42,
        vace_enabled: true  // Enable VACE (this is the default)
      }
    })
  });

  if (!response.ok) {
    throw new Error(`Failed to load pipeline: ${await response.text()}`);
  }

  return await response.json();
}

// Load and wait for pipeline
await loadPipelineWithVACE();
await waitForPipelineReady();  // Same function as Normal Mode
```

## Step 2: Upload Reference Images (Optional)

If you want to combine input video with reference images for style/character guidance, you need to upload them to the server's assets directory.

### Uploading from Browser (File Input)

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

  if (!response.ok) {
    throw new Error(`Upload failed: ${response.statusText}`);
  }

  const result = await response.json();
  console.log("Uploaded reference image:", result.path);
  return result.path;  // Use this path for vace_ref_images
}

// Upload a reference image from file input
const fileInput = document.getElementById("imageInput");
const refImagePath = await uploadReferenceImage(fileInput.files[0]);
```

### Uploading Remotely (From URL or Server-Side)

You can upload images from any source that can make HTTP requests:

```javascript
// Upload from a remote URL
async function uploadImageFromUrl(imageUrl, filename) {
  // Fetch the image from the remote URL
  const imageResponse = await fetch(imageUrl);
  if (!imageResponse.ok) {
    throw new Error(`Failed to fetch image: ${imageResponse.statusText}`);
  }

  const imageBlob = await imageResponse.blob();
  const arrayBuffer = await imageBlob.arrayBuffer();

  // Upload to Scope server
  const response = await fetch(
    `${API_BASE}/api/v1/assets?filename=${encodeURIComponent(filename)}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/octet-stream" },
      body: arrayBuffer
    }
  );

  if (!response.ok) {
    throw new Error(`Upload failed: ${response.statusText}`);
  }

  const result = await response.json();
  return result.path;
}

// Example: Upload image from a URL
const refImagePath = await uploadImageFromUrl(
  "https://example.com/character.png",
  "character.png"
);
```

### Upload from Node.js / Python (Server-Side)

```python
# Python example using requests
import requests

def upload_reference_image(api_base, image_path):
    """Upload a local image file to Scope assets."""
    filename = image_path.split("/")[-1]

    with open(image_path, "rb") as f:
        content = f.read()

    response = requests.post(
        f"{api_base}/api/v1/assets",
        params={"filename": filename},
        headers={"Content-Type": "application/octet-stream"},
        data=content
    )
    response.raise_for_status()

    result = response.json()
    return result["path"]  # Use this path for vace_ref_images

# Example
ref_path = upload_reference_image("http://localhost:8000", "/local/path/to/image.png")
```

```javascript
// Node.js example using fetch
import { readFileSync } from 'fs';

async function uploadReferenceImage(apiBase, imagePath) {
  const filename = imagePath.split('/').pop();
  const content = readFileSync(imagePath);

  const response = await fetch(
    `${apiBase}/api/v1/assets?filename=${encodeURIComponent(filename)}`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/octet-stream' },
      body: content
    }
  );

  if (!response.ok) {
    throw new Error(`Upload failed: ${response.statusText}`);
  }

  const result = await response.json();
  return result.path;
}
```

### Listing Available Assets

```javascript
async function listAssets(type = "image") {
  const response = await fetch(`${API_BASE}/api/v1/assets?type=${type}`);
  return await response.json();
}

// Get all uploaded images
const assets = await listAssets("image");
console.log("Available images:", assets.assets.map(a => a.path));
```

### Supported File Types

From the server ([app.py](../../src/scope/server/app.py#L593-L595)):
- **Images**: `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`
- **Videos**: `.mp4`, `.avi`, `.mov`, `.mkv`, `.webm`
- **Max size**: 50MB

## Step 3: Initialize WebRTC Connection (VACE Mode)

Set up the WebRTC peer connection with VACE parameters:

```javascript
async function startVACEVideoStream(inputStream, initialPrompt, refImagePaths = []) {
  // 1. Get ICE servers from backend
  const iceResponse = await fetch(`${API_BASE}/api/v1/webrtc/ice-servers`);
  const { iceServers } = await iceResponse.json();

  // 2. Create peer connection
  const pc = new RTCPeerConnection({ iceServers });

  // Session state
  let sessionId = null;
  const queuedCandidates = [];

  // 3. Create data channel for parameter updates
  const dataChannel = pc.createDataChannel("parameters", { ordered: true });

  dataChannel.onopen = () => {
    console.log("Data channel opened - ready for VACE parameter updates");
  };

  dataChannel.onmessage = (event) => {
    const data = JSON.parse(event.data);
    if (data.type === "stream_stopped") {
      console.log("Stream stopped:", data.error_message);
      pc.close();
    }
  };

  // 4. Add input video track (will be used for VACE conditioning)
  inputStream.getTracks().forEach((track) => {
    if (track.kind === "video") {
      console.log("Adding video track for VACE conditioning");
      pc.addTrack(track, inputStream);
    }
  });

  // 5. Handle incoming video track (generated output)
  pc.ontrack = (event) => {
    if (event.streams && event.streams[0]) {
      const outputVideo = document.getElementById("outputVideo");
      outputVideo.srcObject = event.streams[0];
      console.log("Receiving VACE-guided video from server");
    }
  };

  // 6. Monitor connection state
  pc.onconnectionstatechange = () => {
    console.log("Connection state:", pc.connectionState);
  };

  // 7. Handle ICE candidates
  pc.onicecandidate = async (event) => {
    if (event.candidate) {
      if (sessionId) {
        await sendIceCandidate(sessionId, event.candidate);
      } else {
        queuedCandidates.push(event.candidate);
      }
    }
  };

  // 8. Create and send offer with VACE parameters
  const offer = await pc.createOffer();
  await pc.setLocalDescription(offer);

  // Build initial parameters for VACE mode
  const initialParameters = {
    input_mode: "video",
    prompts: [{ text: initialPrompt, weight: 1.0 }],
    denoising_step_list: [700, 500],
    vace_enabled: true,           // Enable VACE
    vace_use_input_video: true,   // Use input video for VACE conditioning
    vace_context_scale: 1.0       // Control influence strength (0.0-2.0)
  };

  // Add reference images if provided
  if (refImagePaths.length > 0) {
    initialParameters.vace_ref_images = refImagePaths;
  }

  const response = await fetch(`${API_BASE}/api/v1/webrtc/offer`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      sdp: pc.localDescription.sdp,
      type: pc.localDescription.type,
      initialParameters
    })
  });

  if (!response.ok) {
    throw new Error(`WebRTC offer failed: ${await response.text()}`);
  }

  const answer = await response.json();
  sessionId = answer.sessionId;

  // 9. Set remote description
  await pc.setRemoteDescription({
    type: answer.type,
    sdp: answer.sdp
  });

  // 10. Send queued ICE candidates
  for (const candidate of queuedCandidates) {
    await sendIceCandidate(sessionId, candidate);
  }
  queuedCandidates.length = 0;

  return { pc, dataChannel, sessionId };
}
```

## Step 4: Update VACE Parameters During Streaming

Use the data channel to update VACE-specific parameters:

```javascript
function updateVaceContextScale(dataChannel, scale) {
  // Controls how strongly the input video influences generation
  // 0.0 = No influence (pure text-to-video)
  // 1.0 = Balanced influence (default)
  // 2.0 = Maximum influence
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      vace_context_scale: scale
    }));
  }
}

function updateReferenceImages(dataChannel, imagePaths) {
  // Set new reference images for style/character guidance
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      vace_ref_images: imagePaths,
      vace_context_scale: 1.0
    }));
  }
}

function clearReferenceImages(dataChannel) {
  // Remove reference images
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      vace_ref_images: []
    }));
  }
}

function toggleVaceInputVideo(dataChannel, useInputVideo) {
  // Toggle whether input video is used for VACE conditioning
  // When false, input video is used for latent initialization instead
  // (allows using ref images while still having input video influence latents)
  if (dataChannel.readyState === "open") {
    dataChannel.send(JSON.stringify({
      vace_use_input_video: useInputVideo
    }));
  }
}
```

## Complete VACE Example

```javascript
async function mainVACE() {
  const API_BASE = "http://localhost:8000";

  // 1. Load pipeline with VACE enabled
  console.log("Loading LongLive pipeline with VACE...");
  await loadPipelineWithVACE();
  await waitForPipelineReady();

  // 2. Upload a reference image (optional)
  const refImagePath = "/path/to/reference.png";  // Or use uploadReferenceImage()

  // 3. Get webcam stream (or use a control video like depth/pose)
  console.log("Getting webcam stream...");
  const inputStream = await getWebcamStream();

  // 4. Start VACE video-to-video streaming
  console.log("Starting VACE stream...");
  const { pc, dataChannel, sessionId } = await startVACEVideoStream(
    inputStream,
    "A person in anime style, vibrant colors",
    [refImagePath]  // Optional reference images
  );

  // 5. Adjust VACE influence after 5 seconds
  setTimeout(() => {
    console.log("Increasing VACE influence...");
    updateVaceContextScale(dataChannel, 1.5);
  }, 5000);

  // 6. Change reference image after 10 seconds
  setTimeout(() => {
    console.log("Changing reference image...");
    updateReferenceImages(dataChannel, ["/path/to/new_reference.png"]);
  }, 10000);

  // 7. Stop after 30 seconds
  setTimeout(() => {
    stopStream(pc, dataChannel, sessionId);
    inputStream.getTracks().forEach(track => track.stop());
  }, 30000);
}

mainVACE().catch(console.error);
```

## Key Parameters (VACE Mode)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `vace_enabled` | bool | true | Must be `true` for VACE mode |
| `vace_use_input_video` | bool | true | Route input video to VACE conditioning |
| `vace_ref_images` | array | [] | List of reference image paths |
| `vace_context_scale` | float | 1.0 | Influence strength (0.0-2.0) |
| `input_mode` | string | - | Set to `"video"` for V2V |
| `prompts` | array | - | Text prompts with weights |

## How VACE Mode Works

When VACE is enabled with input video:

1. **VACE Encoding**: Input video is encoded through VaceEncodingBlock to create a 96-channel conditioning tensor (`vace_context`)
2. **Hint Generation**: VACE blocks process the context to generate "hints" - guidance signals for the transformer
3. **Pure Noise Start**: Unlike Normal Mode, latents start as pure Gaussian noise
4. **Hint Injection**: During denoising, hints are injected into transformer blocks as residual connections
5. **Decoding**: Final latents (which only contain generated video, no input frames) are decoded

```
Input Video → VACE Encoding → vace_context → Hint Generation
                                                    ↓
Pure Noise → Latents ←←←←←←←←←← Hint Injection ←←←←←
                ↓
           Denoising (guided by prompts + hints)
                ↓
           Output Video
```

## Use Cases for VACE Mode

### 1. Control Video Guidance (Depth, Pose, Flow)

Use preprocessed control videos to guide generation.

> **Note:** The VACE system does NOT require you to specify the type of control input (depth, pose, flow, etc.). It treats all input as 3-channel RGB conditioning maps. The VACE model learns to interpret the visual structure regardless of the specific control type.
>
> From [vace_encoding.py](../../src/scope/core/pipelines/wan2_1/vace/blocks/vace_encoding.py#L528-L531):
> - `vace_input_frames` = conditioning maps (3-channel RGB from annotators)
> - The system expects any preprocessed control video (depth, pose, scribble, optical flow) already converted to RGB format

```javascript
// Get a depth video from external source or preprocessing
// The depth video should be RGB (3-channel) - grayscale depth maps
// are automatically converted to 3-channel by replicating the channel
const depthVideoStream = getDepthVideoStream();

await startVACEVideoStream(
  depthVideoStream,
  "A futuristic robot walking through a city",
  []  // No ref images, just structural guidance
);
```

**Preprocessing Requirements:**
- Control videos must match the pipeline resolution (e.g., 576x320)
- RGB format (3-channel) - single-channel inputs are auto-converted
- 12 frames per chunk to match output timing
- You can use external tools like:
  - **Depth**: MiDaS, Depth Anything, ZoeDepth
  - **Pose**: OpenPose, MediaPipe, DWPose
  - **Flow**: RAFT, FlowFormer
  - **Edges**: Canny, HED, PiDiNet

### 2. Animate Anything (Reference + Control)

Combine a reference image with a control video:

```javascript
// Reference image defines character/style
const characterRef = "/assets/anime_character.png";

// Pose video defines motion
const poseVideoStream = getPoseVideoStream();

await startVACEVideoStream(
  poseVideoStream,
  "The character walking gracefully",
  [characterRef]
);
```

### 3. Style Transfer with Structure Preservation

Use webcam as structural guidance while applying a reference style:

```javascript
const webcamStream = await getWebcamStream();
const styleRef = "/assets/oil_painting_style.png";

await startVACEVideoStream(
  webcamStream,
  "Oil painting style, impressionist",
  [styleRef]
);
```

---

# Comparison Summary

| Aspect | Normal Mode (VACE OFF) | VACE Mode (VACE ON) |
|--------|----------------------|---------|
| Input video routing | `video` → latent init | `vace_input_frames` → conditioning |
| Latent starting point | Noisy encoded input | Pure noise |
| Influence mechanism | Direct latent blending | Hint injection in transformer |
| Key parameter | `noise_scale` | `vace_context_scale` |
| Reference images | Not with input video | Yes, combinable |
| Processing speed | Faster | Slower |
| Best for | Real-time transformation | Control videos, ref images |

---

---

# Troubleshooting

## Connection Issues
- Ensure the server is running and accessible
- Check that ICE servers are configured (STUN/TURN)
- Verify firewall settings allow WebRTC traffic

## Video Quality Issues
- **Normal Mode**: Adjust `noise_scale` - lower values preserve more input detail
- **VACE Mode**: Adjust `vace_context_scale` - higher values follow input more closely
- Increase denoising steps for higher quality (but slower)
- Ensure input video resolution matches pipeline resolution

## Performance Issues
- Use fewer denoising steps: `[700, 500]` instead of `[1000, 750, 500, 250]`
- Lower resolution in `load_params`
- Use Normal Mode instead of VACE for faster processing
- Ensure GPU has sufficient VRAM
  - **Normal Mode**: Lower VRAM requirements
  - **VACE Mode**: Requires ~48GB VRAM minimum (documented in [mixin.py](../../src/scope/core/pipelines/wan2_1/vace/mixin.py#L14-L16))
