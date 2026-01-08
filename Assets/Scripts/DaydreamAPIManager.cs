using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;
using System.Text;
using TMPro;

/// <summary>
/// Manages the connection to the RunPod LongLive API.
/// Handles pipeline loading, WebRTC signaling (single connection for send/recv),
/// and real-time parameter updates via Data Channel.
/// </summary>
public class DaydreamAPIManager : MonoBehaviour
{
    #region Data Structures

    [Serializable]
    public class PipelineStatusResponse
    {
        public string status;
        public string pipeline_id;
        public string error;
        public PipelineLoadParams load_params;
    }

    [Serializable]
    public class PipelineLoadRequest
    {
        public string pipeline_id;
        public PipelineLoadParams load_params;
    }

    [Serializable]
    public class PipelineLoadParams
    {
        public int height;
        public int width;
        public int seed;
    }

    [Serializable]
    public class IceServersResponse
    {
        public List<IceServerData> iceServers;
    }

    [Serializable]
    public class IceServerData
    {
        public string[] urls;
        public string username;
        public string credential;
    }

    [Serializable]
    public class RunPodParameters
    {
        public string input_mode = "video";
        public List<PromptData> prompts = new List<PromptData>();
        public string prompt_interpolation_method = "slerp";
        public int[] denoising_step_list = new int[] { 1000, 750, 500, 250 };
        public float noise_scale = 0.8f;
        public bool manage_cache = true;
    }

    [Serializable]
    public class PromptData
    {
        public string text;
        public float weight;
    }

    [Serializable]
    public class OfferRequest
    {
        public string sdp;
        public string type;
        public RunPodParameters initialParameters;
    }

    [Serializable]
    public class OfferResponse
    {
        public string sdp;
        public string type;
        public string sessionId;
    }

    [Serializable]
    public class CandidateRequest
    {
        public List<CandidateData> candidates;
    }

    [Serializable]
    public class CandidateData
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    private class CacheResetPayload
    {
        public bool manage_cache;
        public bool reset_cache;
    }

    [Serializable]
    private class DataChannelMessage
    {
        public string type;
        public string error_message;
    }

    #endregion

    #region Inspector Fields

    [Header("API Configuration")]
    [SerializeField] private string runPodBaseUrl = "";

    [Header("Input/Output")]
    [Tooltip("The RenderTexture that provides the input video stream.")]
    [SerializeField] private RenderTexture inputVideoTexture;
    [Tooltip("The RenderTexture to display the received video stream.")]
    [SerializeField] private RenderTexture outputVideoTexture;

    [Header("Stream Settings")]
    [TextArea(3, 10)]
    [SerializeField] private string prompt = "A beautiful cyberpunk cityscape at night";
    [SerializeField] private int width = 576;
    [SerializeField] private int height = 320;
    [SerializeField] private int seed = 42;
    [Tooltip("Steps for denoising (e.g. 1000, 750, 500, 250)")]
    [SerializeField] private int[] denoisingSteps = new int[] { 1000, 750, 500, 250 };
    [Range(0f, 1f)]
    [Tooltip("Noise scale for video mode (0.0-1.0). Higher = more variation, lower = more faithful to input")]
    [SerializeField] private float noiseScale = 0.2f;

    [Header("Live Control")]
    [Tooltip("Click to update parameters while streaming")]
    [SerializeField] private bool updateParameters = false;
    [Tooltip("Click to reset the video generation cache")]
    [SerializeField] private bool resetCache = false;
    [Tooltip("Auto-manage cache (disable for manual control)")]
    [SerializeField] private bool manageCache = true;

    [Header("UI Display")]
    [Tooltip("Optional TextMeshPro text to display the current prompt")]
    [SerializeField] private TMP_Text promptDisplayText;
    [Tooltip("Optional TextMeshPro text to display noise scale and stream status")]
    [SerializeField] private TMP_Text statusDisplayText;

    [Header("Status")]
    [SerializeField] private string status = "Not connected";
    [SerializeField] private bool isStreaming = false;

    #endregion

    #region Properties

    /// <summary>
    /// Returns true if currently streaming video to/from RunPod API.
    /// </summary>
    public bool IsStreaming => isStreaming;

    /// <summary>
    /// Get the current RunPod base URL (with trailing slash removed).
    /// </summary>
    public string GetRunPodUrl() => runPodBaseUrl?.TrimEnd('/');

    /// <summary>
    /// Set the RunPod base URL at runtime.
    /// Note: This should be called before streaming starts.
    /// </summary>
    /// <param name="url">The new RunPod base URL</param>
    public void SetRunPodUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[RunPod] Cannot set empty URL.");
            return;
        }
        
        runPodBaseUrl = url.TrimEnd('/');
        Debug.Log($"[RunPod] Base URL set to: {runPodBaseUrl}");
    }

    #endregion

    #region Private Fields

    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private VideoStreamTrack senderVideoTrack;
    private string sessionId;
    private List<RTCIceCandidate> queuedCandidates = new List<RTCIceCandidate>();
    private Coroutine streamingCoroutine;
    private RunPodParameters pendingInitialParameters = null;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Normalize URL by removing trailing slash to prevent double-slash issues
        if (!string.IsNullOrEmpty(runPodBaseUrl))
        {
            runPodBaseUrl = runPodBaseUrl.TrimEnd('/');
        }
        
        StartCoroutine(WebRTC.Update());
        StartStreaming();
    }

    private void OnDestroy()
    {
        StopStreaming();
    }

    private void OnValidate()
    {
        if (updateParameters)
        {
            updateParameters = false;
            if (Application.isPlaying && isStreaming && dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
            {
                UpdateParameters();
            }
        }
        
        if (resetCache)
        {
            resetCache = false;
            if (Application.isPlaying && isStreaming)
            {
                ResetCache();
            }
        }
    }

    #endregion

    #region Public API

    [ContextMenu("Start Streaming")]
    public void StartStreaming()
    {
        if (isStreaming) return;
        if (streamingCoroutine != null) StopCoroutine(streamingCoroutine);
        streamingCoroutine = StartCoroutine(StreamingWorkflow());
    }

    [ContextMenu("Stop Streaming")]
    public void StopStreaming()
    {
        Debug.Log("[RunPod] Stopping stream...");
        
        if (streamingCoroutine != null)
        {
            StopCoroutine(streamingCoroutine);
            streamingCoroutine = null;
        }

        CleanupWebRTC();
        
        isStreaming = false;
    }

    /// <summary>
    /// Reset the video generation cache.
    /// Use this when starting a new scene or after timeline changes.
    /// </summary>
    [ContextMenu("Reset Cache")]
    public void ResetCache()
    {
        if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("[RunPod] Data channel not open, cannot reset cache.");
            return;
        }

        // Must disable manage_cache to manually reset
        var payload = new CacheResetPayload
        {
            manage_cache = false,
            reset_cache = true
        };

        string json = JsonUtility.ToJson(payload);
        Debug.Log($"[RunPod] Resetting cache: {json}");
        dataChannel.Send(json);

        // Optionally restore manage_cache after reset
        if (manageCache)
        {
            StartCoroutine(RestoreManageCache());
        }
    }

    /// <summary>
    /// Restore manage_cache setting after a brief delay.
    /// </summary>
    private IEnumerator RestoreManageCache()
    {
        yield return new WaitForSeconds(0.1f);
        if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
        {
            string json = "{\"manage_cache\": true}";
            Debug.Log($"[RunPod] Restoring manage_cache: {json}");
            dataChannel.Send(json);
        }
    }

    /// <summary>
    /// Toggle cache management mode.
    /// When disabled, you have manual control via ResetCache().
    /// </summary>
    public void SetManageCache(bool enabled)
    {
        if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("[RunPod] Data channel not open, cannot update manage_cache.");
            return;
        }

        manageCache = enabled;
        string json = $"{{\"manage_cache\": {enabled.ToString().ToLower()}}}";
        Debug.Log($"[RunPod] Setting manage_cache: {json}");
        dataChannel.Send(json);
    }

    /// <summary>
    /// Update the prompt and automatically send the update to the server.
    /// </summary>
    /// <param name="newPrompt">The new prompt text to use</param>
    /// <param name="weight">The weight for the prompt (default: 1.0)</param>
    public void UpdatePrompt(string newPrompt, float weight = 1.0f)
    {
        if (string.IsNullOrEmpty(newPrompt))
        {
            Debug.LogWarning("[RunPod] Cannot update to empty prompt.");
            return;
        }

        Debug.Log($"[RunPod] Updating prompt to: {newPrompt}");
        prompt = newPrompt;
        UpdateParameters();
    }

    /// <summary>
    /// Update the noise scale and automatically send the update to the server.
    /// </summary>
    /// <param name="newNoiseScale">The new noise scale value (0.0-1.0)</param>
    public void SetNoiseScale(float newNoiseScale)
    {
        noiseScale = Mathf.Clamp01(newNoiseScale);
        Debug.Log($"[RunPod] Setting noise scale to: {noiseScale}");
        if (isStreaming && dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
        {
            UpdateParameters();
        }
        else
        {
            // Update UI even if not streaming yet
            UpdateUIDisplays();
        }
    }

    /// <summary>
    /// Update prompt and noise scale together in a single update.
    /// More efficient than calling UpdatePrompt() and SetNoiseScale() separately.
    /// </summary>
    /// <param name="newPrompt">The new prompt text</param>
    /// <param name="weight">The weight for the prompt (default: 1.0)</param>
    /// <param name="newNoiseScale">The new noise scale value (0.0-1.0)</param>
    public void UpdatePromptAndNoiseScale(string newPrompt, float weight, float newNoiseScale)
    {
        if (string.IsNullOrEmpty(newPrompt))
        {
            Debug.LogWarning("[RunPod] Cannot update to empty prompt.");
            return;
        }

        prompt = newPrompt;
        noiseScale = Mathf.Clamp01(newNoiseScale);
        Debug.Log($"[RunPod] Updating prompt to: {newPrompt} with noise scale: {noiseScale}");
        UpdateParameters();
    }

    /// <summary>
    /// Get the current denoising steps array.
    /// </summary>
    public int[] GetDenoisingSteps()
    {
        return denoisingSteps;
    }

    /// <summary>
    /// Set the denoising steps and update parameters.
    /// </summary>
    public void SetDenoisingSteps(int[] steps)
    {
        if (steps != null && steps.Length > 0)
        {
            denoisingSteps = steps;
            Debug.Log($"[RunPod] Setting denoising steps to: [{string.Join(", ", steps)}]");
            if (isStreaming && dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
            {
                UpdateParameters();
            }
        }
    }

    public void UpdateParameters()
    {
        if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("[RunPod] Data channel not open, cannot update parameters.");
            return;
        }

        var paramsObj = new RunPodParameters
        {
            prompts = new List<PromptData> { new PromptData { text = prompt, weight = 1.0f } },
            denoising_step_list = denoisingSteps,
            noise_scale = noiseScale
        };

        // Note: JsonUtility has limitations with nested lists/objects sometimes. 
        // We might need a custom serializer if this fails, but for simple structures it should work.
        // However, JsonUtility cannot serialize top-level lists or dictionaries directly, 
        // but RunPodParameters is a class, so it should be fine.
        // BUT: JsonUtility does NOT support serializing List<T> where T is a custom class unless T is Serializable.
        // PromptData is Serializable.
        
        // Unity's JsonUtility is tricky with arrays/lists. 
        // Let's construct the JSON manually for safety or use a wrapper if needed.
        // Actually, let's try a manual JSON construction for the parameters to be safe and match the exact format.
        
        string json = JsonUtility.ToJson(paramsObj);
        Debug.Log($"[RunPod] Sending update: {json}");
        dataChannel.Send(json);
        
        // Update UI displays
        UpdateUIDisplays();
    }

    /// <summary>
    /// Updates the TextMeshPro UI displays with current prompt, noise scale, and stream status.
    /// </summary>
    private void UpdateUIDisplays()
    {
        // Update prompt display
        if (promptDisplayText != null)
        {
            promptDisplayText.text = prompt;
        }
        
        // Update status display with noise scale, steps, and stream status
        if (statusDisplayText != null)
        {
            statusDisplayText.text = $"Noise Scale: {noiseScale:F2}, Steps: {denoisingSteps[0]}/{denoisingSteps[1]}, Stream Status: {status}";
        }
    }

    /// <summary>
    /// Send custom parameters to the API. Useful for temporary parameter overrides.
    /// </summary>
    public void SendCustomParameters(RunPodParameters customParams)
    {
        if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
        {
            Debug.LogWarning("[RunPod] Data channel not open, cannot send custom parameters.");
            return;
        }

        // Update internal fields for UI display
        if (customParams.prompts != null && customParams.prompts.Count > 0)
        {
            prompt = customParams.prompts[0].text;
        }
        noiseScale = customParams.noise_scale;
        
        string json = JsonUtility.ToJson(customParams);
        Debug.Log($"[RunPod] Sending custom parameters: {json}");
        dataChannel.Send(json);
        
        // Update UI displays
        UpdateUIDisplays();
    }

    /// <summary>
    /// Set initial parameters to be sent when the data channel first opens.
    /// Call this before the data channel opens (e.g., in Start() of another script).
    /// </summary>
    public void SetInitialParameters(RunPodParameters initialParams)
    {
        pendingInitialParameters = initialParams;
        
        // Update internal fields for UI display
        if (initialParams.prompts != null && initialParams.prompts.Count > 0)
        {
            prompt = initialParams.prompts[0].text;
        }
        noiseScale = initialParams.noise_scale;
        
        Debug.Log($"[RunPod] Initial parameters queued: prompt='{initialParams.prompts[0].text}', noise_scale={initialParams.noise_scale}, steps=[{string.Join(", ", initialParams.denoising_step_list)}]");
        
        // Update UI displays immediately
        UpdateUIDisplays();
    }

    #endregion

    #region Core Workflow

    private IEnumerator StreamingWorkflow()
    {
        status = "Checking pipeline status...";
        Debug.Log($"[RunPod] Starting workflow. Status: {status}");

        // 1. Load Pipeline
        yield return LoadPipeline();

        // 2. Get ICE Servers
        status = "Fetching ICE servers...";
        Debug.Log($"[RunPod] {status}");
        var iceServersRequest = UnityWebRequest.Get($"{runPodBaseUrl}/api/v1/webrtc/ice-servers");
        yield return iceServersRequest.SendWebRequest();

        if (iceServersRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RunPod] Failed to get ICE servers: {iceServersRequest.error}");
            status = "Error getting ICE servers";
            yield break;
        }
        Debug.Log($"[RunPod] ICE servers fetched successfully. Response: {iceServersRequest.downloadHandler.text}");

        RTCIceServer[] iceServers = null;
        try
        {
            // The response is { iceServers: [ ... ] }
            // We need to parse this manually or use a wrapper because Unity's JsonUtility is strict.
            // Let's try to parse the wrapper.
            var iceResponse = JsonUtility.FromJson<IceServersResponse>(iceServersRequest.downloadHandler.text);
            
            if (iceResponse != null && iceResponse.iceServers != null)
            {
                iceServers = new RTCIceServer[iceResponse.iceServers.Count];
                for(int i=0; i<iceResponse.iceServers.Count; i++)
                {
                    iceServers[i] = new RTCIceServer
                    {
                        urls = iceResponse.iceServers[i].urls,
                        username = iceResponse.iceServers[i].username,
                        credential = iceResponse.iceServers[i].credential
                    };
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RunPod] Error parsing ICE servers: {e.Message}");
            // Fallback to Google STUN
            iceServers = new RTCIceServer[] { new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } } };
        }

        // 3. Setup WebRTC
        status = "Setting up WebRTC...";
        Debug.Log($"[RunPod] {status}");
        SetupWebRTC(iceServers);

        // 4. Create Offer
        status = "Creating Offer...";
        Debug.Log($"[RunPod] {status}");
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError($"[RunPod] CreateOffer failed: {offerOp.Error.message}");
            yield break;
        }

        var desc = offerOp.Desc;
        var setLocalOp = peerConnection.SetLocalDescription(ref desc);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError($"[RunPod] SetLocalDescription failed: {setLocalOp.Error.message}");
            yield break;
        }

        Debug.Log($"[RunPod] {status}");
        
        // Use pending initial parameters if set, otherwise create default parameters
        RunPodParameters initialParams;
        if (pendingInitialParameters != null)
        {
            initialParams = pendingInitialParameters;
            Debug.Log($"[RunPod] Using pending initial parameters: prompt='{initialParams.prompts[0].text}', noise_scale={initialParams.noise_scale}");
        }
        else
        {
            initialParams = new RunPodParameters
            {
                denoising_step_list = denoisingSteps,
                prompts = new List<PromptData> { new PromptData { text = prompt, weight = 1.0f } },
                noise_scale = noiseScale,
                manage_cache = manageCache
            };
            Debug.Log($"[RunPod] Using default initial parameters: prompt='{prompt}', noise_scale={noiseScale}");
        }

        // Use JsonUtility for safe serialization including proper string escaping
        var offerRequest = new OfferRequest
        {
            sdp = desc.sdp,
            type = desc.type.ToString().ToLower(),
            initialParameters = initialParams
        };

        string offerJson = JsonUtility.ToJson(offerRequest);
        Debug.Log($"[RunPod] Generated Offer JSON (first 100 chars): {offerJson.Substring(0, Math.Min(100, offerJson.Length))}...");
        
        var offerReq = new UnityWebRequest($"{runPodBaseUrl}/api/v1/webrtc/offer", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(offerJson);
        offerReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
        offerReq.downloadHandler = new DownloadHandlerBuffer();
        offerReq.SetRequestHeader("Content-Type", "application/json");

        yield return offerReq.SendWebRequest();

        if (offerReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RunPod] Offer failed: {offerReq.error}\n{offerReq.downloadHandler.text}");
            status = "Error sending offer";
            yield break;
        }
        Debug.Log($"[RunPod] Offer accepted. Response: {offerReq.downloadHandler.text}");

        // 6. Handle Answer
        var offerResponse = JsonUtility.FromJson<OfferResponse>(offerReq.downloadHandler.text);
        sessionId = offerResponse.sessionId;
        Debug.Log($"[RunPod] Received Answer. Session ID: {sessionId}");

        var remoteDesc = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = offerResponse.sdp
        };

        var setRemoteOp = peerConnection.SetRemoteDescription(ref remoteDesc);
        yield return setRemoteOp;

        if (setRemoteOp.IsError)
        {
            Debug.LogError($"[RunPod] SetRemoteDescription failed: {setRemoteOp.Error.message}");
            yield break;
        }

        // 7. Send Queued Candidates
        if (queuedCandidates.Count > 0)
        {
            yield return StartCoroutine(SendIceCandidates(queuedCandidates));
            queuedCandidates.Clear();
        }

        status = "Streaming!";
        isStreaming = true;
        UpdateUIDisplays();
    }
    private IEnumerator LoadPipeline()
    {
        Debug.Log($"[RunPod] Checking pipeline status at {runPodBaseUrl}/api/v1/pipeline/status");
        // Check status first
        var statusReq = UnityWebRequest.Get($"{runPodBaseUrl}/api/v1/pipeline/status");
        yield return statusReq.SendWebRequest();

        bool needsLoad = true;
        if (statusReq.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[RunPod] Status check response: {statusReq.downloadHandler.text}");
            var statusData = JsonUtility.FromJson<PipelineStatusResponse>(statusReq.downloadHandler.text);
            if (statusData.status == "loaded" && statusData.pipeline_id == "longlive")
            {
                // Check if resolution matches our desired settings
                if (statusData.load_params != null && 
                    statusData.load_params.width == width && 
                    statusData.load_params.height == height)
                {
                    Debug.Log($"[RunPod] Pipeline already loaded with correct resolution ({width}x{height}).");
                    needsLoad = false;
                }
                else
                {
                    Debug.Log($"[RunPod] Pipeline loaded but resolution mismatch. Current: {statusData.load_params?.width}x{statusData.load_params?.height}, Required: {width}x{height}. Reloading...");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[RunPod] Status check failed: {statusReq.error}");
        }

        if (needsLoad)
        {
            Debug.Log("[RunPod] Loading pipeline...");
            var loadReqData = new PipelineLoadRequest
            {
                pipeline_id = "longlive",
                load_params = new PipelineLoadParams { height = height, width = width, seed = seed }
            };
            
            string loadJson = JsonUtility.ToJson(loadReqData);
            Debug.Log($"[RunPod] Sending load request: {loadJson}");
            var loadReq = new UnityWebRequest($"{runPodBaseUrl}/api/v1/pipeline/load", "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(loadJson);
            loadReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            loadReq.downloadHandler = new DownloadHandlerBuffer();
            loadReq.SetRequestHeader("Content-Type", "application/json");
            
            yield return loadReq.SendWebRequest();

            // Poll for status
            int attempts = 0;
            while (attempts < 60)
            {
                yield return new WaitForSeconds(2f);
                var pollReq = UnityWebRequest.Get($"{runPodBaseUrl}/api/v1/pipeline/status");
                yield return pollReq.SendWebRequest();
                
                if (pollReq.result == UnityWebRequest.Result.Success)
                {
                    var pollData = JsonUtility.FromJson<PipelineStatusResponse>(pollReq.downloadHandler.text);
                    Debug.Log($"[RunPod] Pipeline status: {pollData.status}");
                    if (pollData.status == "loaded") break;
                    if (pollData.status == "error") throw new Exception($"Pipeline error: {pollData.error}");
                }
                attempts++;
            }
        }
    }

    private void SetupWebRTC(RTCIceServer[] iceServers)
    {
        CleanupWebRTC();

        var config = new RTCConfiguration { iceServers = iceServers };
        peerConnection = new RTCPeerConnection(ref config);

        peerConnection.OnIceCandidate = candidate =>
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                StartCoroutine(SendIceCandidates(new List<RTCIceCandidate> { candidate }));
            }
            else
            {
                queuedCandidates.Add(candidate);
            }
        };

        peerConnection.OnIceConnectionChange = state => Debug.Log($"[RunPod] ICE State: {state}");
        peerConnection.OnConnectionStateChange = state => 
        {
            Debug.Log($"[RunPod] Connection State: {state}");
            if (state == RTCPeerConnectionState.Disconnected || state == RTCPeerConnectionState.Failed)
            {
                isStreaming = false;
                status = "Disconnected";
                UpdateUIDisplays();
            }
        };

        peerConnection.OnTrack = e =>
        {
            if (e.Track.Kind == TrackKind.Video)
            {
                Debug.Log("[RunPod] Received video track");
                if (e.Track is VideoStreamTrack track)
                {
                    Debug.Log("[RunPod] Starting video texture update loop");
                    StartCoroutine(UpdateVideoTexture(track));
                }
            }
        };

        // Create Data Channel
        RTCDataChannelInit dcInit = new RTCDataChannelInit { ordered = true };
        dataChannel = peerConnection.CreateDataChannel("parameters", dcInit);
        dataChannel.OnOpen = () => 
        {
            Debug.Log("[RunPod] Data channel open");
            // Initial parameters were already sent with the WebRTC offer.
            // Clear pending parameters to avoid duplicate sends.
            if (pendingInitialParameters != null)
            {
                Debug.Log("[RunPod] Initial parameters already sent with offer, clearing pending");
                pendingInitialParameters = null;
            }
        };
        
        dataChannel.OnMessage = bytes =>
        {
            try
            {
                string message = Encoding.UTF8.GetString(bytes);
                Debug.Log($"[RunPod] Data channel message: {message}");
                
                var data = JsonUtility.FromJson<DataChannelMessage>(message);
                if (data != null && data.type == "stream_stopped")
                {
                    Debug.LogWarning($"[RunPod] Stream stopped by server: {data.error_message ?? "Unknown error"}");
                    status = $"Stream stopped: {data.error_message ?? "Unknown error"}";
                    isStreaming = false;
                    // Optionally close the connection
                    // CleanupWebRTC();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunPod] Failed to parse data channel message: {e.Message}");
            }
        };

        // Add Local Video Track
        if (inputVideoTexture != null)
        {
            senderVideoTrack = new VideoStreamTrack(inputVideoTexture);
            peerConnection.AddTrack(senderVideoTrack);
        }
    }

    private IEnumerator SendIceCandidates(List<RTCIceCandidate> candidates)
    {
        var candidateList = new List<CandidateData>();
        foreach (var c in candidates)
        {
            candidateList.Add(new CandidateData 
            { 
                candidate = c.Candidate, 
                sdpMid = c.SdpMid, 
                sdpMLineIndex = c.SdpMLineIndex.HasValue ? c.SdpMLineIndex.Value : 0 
            });
        }

        var reqBody = new CandidateRequest { candidates = candidateList };
        string json = JsonUtility.ToJson(reqBody); // This might fail for list wrapper, let's check wrapper
        // JsonUtility needs a top level object. CandidateRequest is that object.
        
        var req = new UnityWebRequest($"{runPodBaseUrl}/api/v1/webrtc/offer/{sessionId}", "PATCH");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RunPod] Failed to send ICE candidates: {req.error}");
        }
        else
        {
            Debug.Log("[RunPod] ICE candidates sent successfully.");
        }
    }

    private IEnumerator UpdateVideoTexture(VideoStreamTrack videoTrack)
    {
        while (videoTrack != null && outputVideoTexture != null)
        {
            if (videoTrack.Texture != null)
            {
                Graphics.Blit(videoTrack.Texture, outputVideoTexture);
            }
            yield return new WaitForEndOfFrame();
        }
    }

    private void CleanupWebRTC()
    {
        senderVideoTrack?.Dispose();
        senderVideoTrack = null;
        
        dataChannel?.Close();
        dataChannel?.Dispose();
        dataChannel = null;

        peerConnection?.Close();
        peerConnection?.Dispose();
        peerConnection = null;
        
        sessionId = null;
        queuedCandidates.Clear();
    }

    #endregion

    #region JSON Helpers

    // Helper methods removed as we are using JsonUtility now

    #endregion
}
