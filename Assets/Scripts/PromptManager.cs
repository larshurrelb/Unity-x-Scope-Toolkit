using UnityEngine;
using StarterAssets;

/// <summary>
/// Manages prompt updates and cache resets for the Daydream API.
/// This script provides a simple interface to control the DaydreamAPIManager.
/// </summary>
public class PromptManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the DaydreamAPIManager component")]
    [SerializeField] private DaydreamAPIManager apiManager;
    [Tooltip("Reference to the ThirdPersonController to track character state")]
    [SerializeField] private ThirdPersonController characterController;

    [Header("Dynamic Prompt Parts")]
    [Tooltip("The prefix that comes before the action (e.g. 'A cyberpunk scene with')")]
    [SerializeField] private string promptPrefix = "A 3D animated scene with";
    [Tooltip("The action text (automatically updated based on character state)")]
    [SerializeField] private string promptAction = "person standing";
    [Tooltip("The suffix that comes after the action (e.g. 'in a futuristic city')")]
    [SerializeField] private string promptSuffix = "";

    [Header("Action Descriptions")]
    [Tooltip("Action text when character is standing still")]
    [SerializeField] private string standingAction = "person in coat standing";
    [Tooltip("Action text when character is walking")]
    [SerializeField] private string walkingAction = "person in coat is walking";
    [Tooltip("Action text when character is sprinting")]
    [SerializeField] private string sprintingAction = "person in coat is running";
    [Tooltip("Action text when character is jumping")]
    [SerializeField] private string jumpingAction = "person in coat is jumping";

    [Header("Prompt Configuration")]
    [Range(0f, 1f)]
    [Tooltip("Weight of the prompt (0-1)")]
    [SerializeField] private float promptWeight = 1.0f;
    [Range(0f, 1f)]
    [Tooltip("Noise scale for video mode (0.0-1.0). Higher = more variation, lower = more faithful to input")]
    [SerializeField] private float noiseScale = 0.2f;
    [Tooltip("Auto-update prompt when character state changes")]
    [SerializeField] private bool autoUpdate = true;
    [Tooltip("Minimum time between updates (seconds)")]
    [SerializeField] private float updateCooldown = 0.5f;

    [Header("Dynamic Noise")]
    [Tooltip("Enable dynamic noise scaling based on character movement")]
    [SerializeField] private bool useDynamicNoise = false;
    [Range(0f, 1f)]
    [Tooltip("Noise scale when character is standing still")]
    [SerializeField] private float standingNoiseScale = 0.1f;
    [Range(0f, 1f)]
    [Tooltip("Noise scale when character is moving, running, or jumping")]
    [SerializeField] private float movingNoiseScale = 0.4f;

    [Header("Visible Objects Prompt")]
    [Tooltip("Enable visible objects prompt that describes nearby tagged objects")]
    [SerializeField] private bool useVisibleObjectsPrompt = false;
    [Tooltip("The camera used to determine left/right positioning of objects")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Maximum distance to search for visible objects")]
    [SerializeField] private float maxVisibleDistance = 50f;
    [Tooltip("Distance threshold to separate foreground from background objects")]
    [SerializeField] private float foregroundThreshold = 15f;
    [Tooltip("Tag used to identify visible objects (default: VisibleObject)")]
    [SerializeField] private string visibleObjectTag = "VisibleObject";

    [Header("Actions")]
    [Tooltip("Click to update the prompt")]
    [SerializeField] private bool updatePrompt = false;
    [Tooltip("Click to reset the cache")]
    [SerializeField] private bool resetCache = false;

    [Header("Debug Info")]
    [SerializeField] private string currentFullPrompt = "";
    [SerializeField] private string currentState = "Unknown";
    [SerializeField] private string currentVisibleObjectsPrompt = "";

    private string lastAction = "";
    private string lastVisibleObjectsPrompt = "";
    private float lastUpdateTime = 0f;
    private float currentNoiseScale = 0.2f;
    private float targetNoiseScale = 0.2f;
    private float startNoiseScale = 0.2f;
    private float noiseTransitionStartTime = 0f;
    private const float noiseTransitionDuration = 2f;
    private string defaultPrefix = "";
    private string defaultSuffix = "";
    
    // External noise scale override (used by trigger zones)
    private bool hasExternalNoiseOverride = false;
    private float externalNoiseScale = 0f;

    private void OnValidate()
    {
        if (updatePrompt)
        {
            updatePrompt = false;
            if (Application.isPlaying)
            {
                UpdatePrompt();
            }
        }

        if (resetCache)
        {
            resetCache = false;
            if (Application.isPlaying)
            {
                ResetCache();
            }
        }
    }

    private void Start()
    {
        // Store the default prefix and suffix for zone resets
        defaultPrefix = promptPrefix;
        defaultSuffix = promptSuffix;
        
        // Auto-find the API manager if not assigned
        if (apiManager == null)
        {
            apiManager = FindObjectOfType<DaydreamAPIManager>();
            if (apiManager == null)
            {
                Debug.LogError("[PromptManager] No DaydreamAPIManager found in scene!");
            }
        }

        // Auto-find the character controller if not assigned
        if (characterController == null)
        {
            characterController = FindObjectOfType<ThirdPersonController>();
            if (characterController == null)
            {
                Debug.LogWarning("[PromptManager] No ThirdPersonController found in scene! Auto-update disabled.");
                autoUpdate = false;
            }
        }

        // Auto-find the player camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null && useVisibleObjectsPrompt)
            {
                Debug.LogWarning("[PromptManager] No Camera found! Visible objects prompt will not work correctly.");
            }
        }

        // Initialize with standing state
        lastAction = standingAction;
        promptAction = standingAction;
        
        // Initialize noise scale
        currentNoiseScale = useDynamicNoise ? standingNoiseScale : noiseScale;
        targetNoiseScale = currentNoiseScale;
        
        UpdateFullPrompt();
    }

    private void Update()
    {
        if (!autoUpdate || characterController == null) return;

        // Determine current action based on character state
        string newAction = DetermineAction();

        // Determine appropriate noise scale - external override takes priority
        float newTargetNoiseScale;
        if (hasExternalNoiseOverride)
        {
            newTargetNoiseScale = externalNoiseScale;
        }
        else
        {
            newTargetNoiseScale = useDynamicNoise ? GetDynamicNoiseScale() : noiseScale;
        }
        
        // Check if target noise scale changed
        if (Mathf.Abs(targetNoiseScale - newTargetNoiseScale) > 0.001f)
        {
            startNoiseScale = currentNoiseScale;
            targetNoiseScale = newTargetNoiseScale;
            noiseTransitionStartTime = Time.time;
        }
        
        // Smoothly transition current noise scale towards target
        if (Mathf.Abs(currentNoiseScale - targetNoiseScale) > 0.001f)
        {
            float elapsed = Time.time - noiseTransitionStartTime;
            float t = Mathf.Clamp01(elapsed / noiseTransitionDuration);
            currentNoiseScale = Mathf.Lerp(startNoiseScale, targetNoiseScale, t);
            
            // If we're close enough to target, snap to it
            if (t >= 1f)
            {
                currentNoiseScale = targetNoiseScale;
            }
        }

        // Check cooldown for prompt updates
        bool canUpdate = Time.time - lastUpdateTime >= updateCooldown;
        
        // Update if action changed or we need to send noise scale update
        bool actionChanged = newAction != lastAction;
        bool noiseScaleUpdated = Mathf.Abs(currentNoiseScale - targetNoiseScale) < 0.001f && Time.time - noiseTransitionStartTime < noiseTransitionDuration + 0.1f;
        
        // Check if visible objects prompt changed
        string newVisibleObjectsPrompt = useVisibleObjectsPrompt ? BuildVisibleObjectsPrompt() : "";
        bool visibleObjectsChanged = newVisibleObjectsPrompt != lastVisibleObjectsPrompt;
        
        if (canUpdate && (actionChanged || noiseScaleUpdated || visibleObjectsChanged))
        {
            promptAction = newAction;
            lastAction = newAction;
            lastVisibleObjectsPrompt = newVisibleObjectsPrompt;
            currentVisibleObjectsPrompt = newVisibleObjectsPrompt;
            UpdateFullPrompt();
            UpdatePrompt();
            lastUpdateTime = Time.time;
        }
    }

    private string DetermineAction()
    {
        // Check if jumping (highest priority)
        if (!characterController.Grounded)
        {
            currentState = "Jumping/Falling";
            return jumpingAction;
        }

        // Check if sprinting
        if (characterController.MoveSpeed > 0 && Mathf.Abs(characterController.SprintSpeed - GetCurrentSpeed()) < 0.1f)
        {
            currentState = "Sprinting";
            return sprintingAction;
        }

        // Check if walking
        if (GetCurrentSpeed() > 0.1f)
        {
            currentState = "Walking";
            return walkingAction;
        }

        // Standing still
        currentState = "Standing";
        return standingAction;
    }

    private float GetDynamicNoiseScale()
    {
        // Return standing noise scale if character is standing, otherwise return moving noise scale
        if (currentState == "Standing")
        {
            return standingNoiseScale;
        }
        else
        {
            return movingNoiseScale;
        }
    }

    private float GetCurrentSpeed()
    {
        // Get the character controller component to check actual velocity
        CharacterController cc = characterController.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 horizontalVelocity = new Vector3(cc.velocity.x, 0, cc.velocity.z);
            return horizontalVelocity.magnitude;
        }
        return 0f;
    }

    private void UpdateFullPrompt()
    {
        // Combine prefix + action + visible objects + suffix
        currentFullPrompt = $"{promptPrefix} {promptAction}";
        
        // Add visible objects prompt if enabled
        if (useVisibleObjectsPrompt && !string.IsNullOrEmpty(currentVisibleObjectsPrompt))
        {
            currentFullPrompt += $". {currentVisibleObjectsPrompt}";
        }
        
        if (!string.IsNullOrEmpty(promptSuffix))
        {
            currentFullPrompt += $" {promptSuffix}";
        }
        currentFullPrompt = currentFullPrompt.Trim();
    }

    /// <summary>
    /// Builds a prompt describing visible objects near the player.
    /// Objects are categorized by distance (foreground/background) and position (top/middle/bottom, left/center/right).
    /// </summary>
    private string BuildVisibleObjectsPrompt()
    {
        if (characterController == null || playerCamera == null)
        {
            return "";
        }

        // Find all objects with the visible object tag
        GameObject[] visibleObjects = GameObject.FindGameObjectsWithTag(visibleObjectTag);
        if (visibleObjects == null || visibleObjects.Length == 0)
        {
            return "";
        }

        Vector3 playerPosition = characterController.transform.position;
        Vector3 cameraForward = playerCamera.transform.forward;
        Vector3 cameraRight = playerCamera.transform.right;
        Vector3 cameraUp = playerCamera.transform.up;

        // Threshold for determining center vs left/right and middle vs top/bottom
        const float horizontalCenterThreshold = 0.25f;
        const float verticalCenterThreshold = 0.25f;

        // Dictionary to categorize objects by position key
        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> foregroundObjects = 
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> backgroundObjects = 
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

        foreach (GameObject obj in visibleObjects)
        {
            if (obj == null) continue;

            Vector3 toObject = obj.transform.position - playerPosition;
            float distance = toObject.magnitude;

            // Skip objects that are too far
            if (distance > maxVisibleDistance) continue;

            // Skip objects behind the camera
            float dotForward = Vector3.Dot(toObject.normalized, cameraForward);
            if (dotForward < 0) continue;

            // Determine horizontal position relative to camera (left/center/right)
            float dotRight = Vector3.Dot(toObject.normalized, cameraRight);
            string horizontalPos;
            if (Mathf.Abs(dotRight) < horizontalCenterThreshold)
                horizontalPos = "center";
            else if (dotRight > 0)
                horizontalPos = "right";
            else
                horizontalPos = "left";

            // Determine vertical position relative to camera (top/middle/bottom)
            float dotUp = Vector3.Dot(toObject.normalized, cameraUp);
            string verticalPos;
            if (Mathf.Abs(dotUp) < verticalCenterThreshold)
                verticalPos = "middle";
            else if (dotUp > 0)
                verticalPos = "top";
            else
                verticalPos = "bottom";

            // Build position key
            string positionKey = GetPositionDescription(verticalPos, horizontalPos);

            // Determine foreground or background
            bool isForeground = distance <= foregroundThreshold;

            // Get the object name
            string objectName = obj.name;

            // Add to appropriate dictionary
            var targetDict = isForeground ? foregroundObjects : backgroundObjects;
            if (!targetDict.ContainsKey(positionKey))
            {
                targetDict[positionKey] = new System.Collections.Generic.List<string>();
            }
            targetDict[positionKey].Add(objectName);
        }

        // Build the prompt string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Foreground description
        if (foregroundObjects.Count > 0)
        {
            sb.Append("In the foreground:");
            sb.Append(BuildPositionDescription(foregroundObjects));
        }

        // Background description
        if (backgroundObjects.Count > 0)
        {
            if (foregroundObjects.Count > 0) sb.Append(". ");
            sb.Append("In the background:");
            sb.Append(BuildPositionDescription(backgroundObjects));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a human-readable position description from vertical and horizontal positions.
    /// </summary>
    private string GetPositionDescription(string verticalPos, string horizontalPos)
    {
        // Handle center/middle case
        if (verticalPos == "middle" && horizontalPos == "center")
            return "in the center";
        
        // Handle pure horizontal positions (middle vertical)
        if (verticalPos == "middle")
            return $"on the {horizontalPos}";
        
        // Handle pure vertical positions (center horizontal)
        if (horizontalPos == "center")
            return $"at the {verticalPos}";
        
        // Combined positions (e.g., "top left", "bottom right")
        return $"at the {verticalPos} {horizontalPos}";
    }

    /// <summary>
    /// Builds a description string from a dictionary of position -> object names.
    /// </summary>
    private string BuildPositionDescription(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> objectsByPosition)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Define order for positions to ensure consistent output
        string[] positionOrder = new string[]
        {
            "at the top left", "at the top", "at the top right",
            "on the left", "in the center", "on the right",
            "at the bottom left", "at the bottom", "at the bottom right"
        };

        bool first = true;
        foreach (string position in positionOrder)
        {
            if (objectsByPosition.ContainsKey(position) && objectsByPosition[position].Count > 0)
            {
                if (!first) sb.Append(";");
                sb.Append($" {position} {string.Join(", ", objectsByPosition[position])}");
                first = false;
            }
        }

        // Handle any positions not in the predefined order (shouldn't happen, but just in case)
        foreach (var kvp in objectsByPosition)
        {
            if (!System.Array.Exists(positionOrder, p => p == kvp.Key) && kvp.Value.Count > 0)
            {
                if (!first) sb.Append(";");
                sb.Append($" {kvp.Key} {string.Join(", ", kvp.Value)}");
                first = false;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Update the prompt on the API manager.
    /// </summary>
    [ContextMenu("Update Prompt")]
    public void UpdatePrompt()
    {
        if (apiManager == null)
        {
            Debug.LogError("[PromptManager] API Manager reference is null!");
            return;
        }

        UpdateFullPrompt();

        if (string.IsNullOrEmpty(currentFullPrompt))
        {
            Debug.LogWarning("[PromptManager] Prompt text is empty!");
            return;
        }

        // Use dynamic noise scale if enabled, otherwise use static noise scale
        float effectiveNoiseScale = useDynamicNoise ? currentNoiseScale : noiseScale;
        Debug.Log($"[PromptManager] Updating prompt: {currentFullPrompt} with noise scale: {effectiveNoiseScale}");
        // Use batched update to avoid sending two separate updates
        apiManager.UpdatePromptAndNoiseScale(currentFullPrompt, promptWeight, effectiveNoiseScale);
    }

    /// <summary>
    /// Get the current prefix.
    /// </summary>
    public string GetPrefix()
    {
        return promptPrefix;
    }

    /// <summary>
    /// Get the current suffix.
    /// </summary>
    public string GetSuffix()
    {
        return promptSuffix;
    }

    /// <summary>
    /// Get the default prefix (initial value at startup).
    /// </summary>
    public string GetDefaultPrefix()
    {
        return defaultPrefix;
    }

    /// <summary>
    /// Get the default suffix (initial value at startup).
    /// </summary>
    public string GetDefaultSuffix()
    {
        return defaultSuffix;
    }

    /// <summary>
    /// Update the prompt prefix.
    /// </summary>
    public void SetPrefix(string newPrefix)
    {
        promptPrefix = newPrefix;
        UpdateFullPrompt();
        if (autoUpdate)
        {
            UpdatePrompt();
        }
    }

    /// <summary>
    /// Update the prompt suffix.
    /// </summary>
    public void SetSuffix(string newSuffix)
    {
        promptSuffix = newSuffix;
        UpdateFullPrompt();
        if (autoUpdate)
        {
            UpdatePrompt();
        }
    }

    /// <summary>
    /// Manually set the action (overrides auto-update temporarily).
    /// </summary>
    public void SetAction(string newAction)
    {
        promptAction = newAction;
        lastAction = newAction;
        UpdateFullPrompt();
        UpdatePrompt();
    }

    /// <summary>
    /// Reset the video generation cache.
    /// </summary>
    [ContextMenu("Reset Cache")]
    public void ResetCache()
    {
        if (apiManager == null)
        {
            Debug.LogError("[PromptManager] API Manager reference is null!");
            return;
        }

        Debug.Log("[PromptManager] Resetting cache...");
        apiManager.ResetCache();
    }

    /// <summary>
    /// Set the API manager reference.
    /// </summary>
    /// <param name="manager">The DaydreamAPIManager instance</param>
    public void SetAPIManager(DaydreamAPIManager manager)
    {
        apiManager = manager;
    }

    /// <summary>
    /// Update the noise scale.
    /// </summary>
    /// <param name="newNoiseScale">The new noise scale value (0-1)</param>
    public void SetNoiseScale(float newNoiseScale)
    {
        noiseScale = Mathf.Clamp01(newNoiseScale);
        // Also update the dynamic noise values to reflect the override
        currentNoiseScale = noiseScale;
        targetNoiseScale = noiseScale;
        if (apiManager != null)
        {
            Debug.Log($"[PromptManager] Setting noise scale: {noiseScale}");
            apiManager.SetNoiseScale(noiseScale);
        }
    }

    /// <summary>
    /// Get the current noise scale.
    /// </summary>
    public float GetNoiseScale()
    {
        return noiseScale;
    }

    /// <summary>
    /// Set an external noise scale override (used by trigger zones like PrefixChanger/SuffixChanger).
    /// This takes priority over dynamic noise and the base noise scale setting.
    /// </summary>
    /// <param name="overrideValue">The noise scale value to use</param>
    public void SetExternalNoiseOverride(float overrideValue)
    {
        hasExternalNoiseOverride = true;
        externalNoiseScale = Mathf.Clamp01(overrideValue);
        
        // Immediately apply the override
        currentNoiseScale = externalNoiseScale;
        targetNoiseScale = externalNoiseScale;
        
        if (apiManager != null)
        {
            Debug.Log($"[PromptManager] External noise scale override set to: {externalNoiseScale}");
            apiManager.SetNoiseScale(externalNoiseScale);
        }
    }

    /// <summary>
    /// Clear the external noise scale override, returning to normal behavior.
    /// </summary>
    public void ClearExternalNoiseOverride()
    {
        if (!hasExternalNoiseOverride) return;
        
        hasExternalNoiseOverride = false;
        
        // Reset to the appropriate noise scale based on current settings
        float normalNoiseScale = useDynamicNoise ? GetDynamicNoiseScale() : noiseScale;
        currentNoiseScale = normalNoiseScale;
        targetNoiseScale = normalNoiseScale;
        
        if (apiManager != null)
        {
            Debug.Log($"[PromptManager] External noise scale override cleared, returning to: {normalNoiseScale}");
            apiManager.SetNoiseScale(normalNoiseScale);
        }
    }

    /// <summary>
    /// Check if an external noise scale override is currently active.
    /// </summary>
    public bool HasExternalNoiseOverride()
    {
        return hasExternalNoiseOverride;
    }

    /// <summary>
    /// Enable or disable auto-update of prompts based on character state.
    /// </summary>
    /// <param name="enabled">Whether auto-update should be enabled</param>
    public void SetAutoUpdate(bool enabled)
    {
        autoUpdate = enabled;
    }

    /// <summary>
    /// Get the current auto-update state.
    /// </summary>
    public bool GetAutoUpdate()
    {
        return autoUpdate;
    }

    /// <summary>
    /// Get the current denoising steps.
    /// </summary>
    public int[] GetDenoisingSteps()
    {
        if (apiManager != null)
        {
            return apiManager.GetDenoisingSteps();
        }
        return null;
    }

    /// <summary>
    /// Set the denoising steps.
    /// </summary>
    public void SetDenoisingSteps(int[] steps)
    {
        if (apiManager != null)
        {
            apiManager.SetDenoisingSteps(steps);
        }
    }

    /// <summary>
    /// Enable or disable the visible objects prompt feature.
    /// </summary>
    /// <param name="enabled">Whether visible objects prompt should be enabled</param>
    public void SetVisibleObjectsPrompt(bool enabled)
    {
        useVisibleObjectsPrompt = enabled;
        if (!enabled)
        {
            currentVisibleObjectsPrompt = "";
            lastVisibleObjectsPrompt = "";
        }
        UpdateFullPrompt();
        if (autoUpdate)
        {
            UpdatePrompt();
        }
    }

    /// <summary>
    /// Get the current visible objects prompt state.
    /// </summary>
    public bool GetVisibleObjectsPrompt()
    {
        return useVisibleObjectsPrompt;
    }

    /// <summary>
    /// Set the foreground distance threshold for visible objects.
    /// </summary>
    /// <param name="threshold">Distance in units - objects closer than this are foreground</param>
    public void SetForegroundThreshold(float threshold)
    {
        foregroundThreshold = Mathf.Max(0f, threshold);
    }

    /// <summary>
    /// Set the maximum distance to search for visible objects.
    /// </summary>
    /// <param name="maxDistance">Maximum distance in units</param>
    public void SetMaxVisibleDistance(float maxDistance)
    {
        maxVisibleDistance = Mathf.Max(0f, maxDistance);
    }

    /// <summary>
    /// Set the camera used for determining object positions.
    /// </summary>
    /// <param name="camera">The camera to use</param>
    public void SetPlayerCamera(Camera camera)
    {
        playerCamera = camera;
    }

    /// <summary>
    /// Get the current visible objects prompt string (for debugging).
    /// </summary>
    public string GetCurrentVisibleObjectsPrompt()
    {
        return currentVisibleObjectsPrompt;
    }
}
