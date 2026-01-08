using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Enables character-specific chat interactions when the player enters a trigger zone.
/// Shows a prompt to start chat, handles image generation prompt replacement,
/// and manages Gemini chat with a custom system prompt.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CharacterChat : MonoBehaviour
{
    #region Image Generation Settings
    
    [Header("Image Generation Prompt Settings")]
    [Tooltip("The complete prompt to use for image generation when player is inside this area")]
    [TextArea(3, 10)]
    [SerializeField] private string replacementPrompt = "A character conversation scene";
    
    [Header("Noise Settings")]
    [Range(0f, 1f)]
    [Tooltip("Noise scale to use when player is inside this area (0.0-1.0)")]
    [SerializeField] private float replacementNoiseScale = 0.5f;
    
    [Header("Denoising Settings")]
    [Tooltip("Replacement value for denoising step at index 2")]
    [SerializeField] private int replacementDenoisingStep2 = 900;
    
    #endregion
    
    #region Gemini Chat Settings
    
    [Header("Gemini Chat Settings")]
    [Tooltip("The name of this character to display in the chat UI")]
    [SerializeField] private string characterName = "Character";
    
    [Tooltip("Custom system prompt for this character's Gemini chat. Overrides the default system prompt.")] 
    [TextArea(5, 15)]
    [SerializeField] private string characterSystemPrompt = "You are a friendly character in a game world. Respond in character.";
    
    #endregion
    
    #region UI Settings
    
    [Header("Chat Interaction UI")]
    [Tooltip("TextMeshPro text to show when player can start chat (e.g., 'Click to chat')")]
    [SerializeField] private TMP_Text interactionPromptText;
    
    [Tooltip("The message to display when player can interact")]
    [SerializeField] private string interactionMessage = "Click to chat";
    
    [Tooltip("TextMeshPro text in the chat UI to display the character's name")]
    [SerializeField] private TMP_Text characterNameText;
    
    #endregion
    
    #region Behavior Settings
    
    [Header("Behavior")]
    [Tooltip("Disable auto-update while player is in zone")]
    [SerializeField] private bool disableAutoUpdate = true;
    
    [Tooltip("Restore defaults when player exits")]
    [SerializeField] private bool restoreOnExit = true;
    
    [Header("Detection Settings")]
    [Tooltip("Tag to detect for player (default: Player)")]
    [SerializeField] private string playerTag = "Player";
    
    #endregion
    
    #region References
    
    [Header("References")]
    [Tooltip("Reference to the PromptManager (auto-finds if not set)")]
    [SerializeField] private PromptManager promptManager;
    
    [Tooltip("Reference to the InputSwitcher for UI mode control (auto-finds if not set)")]
    [SerializeField] private InputSwitcher inputSwitcher;
    
    [Tooltip("Reference to the GeminiChatManager (auto-finds if not set)")]
    [SerializeField] private GeminiChatManager geminiChatManager;
    
    #endregion
    
    #region Debug
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    #endregion
    
    #region Private Fields
    
    private bool isPlayerInside = false;
    private bool isInChatMode = false;
    private string originalPrefix = "";
    private string originalSuffix = "";
    private bool originalAutoUpdate = true;
    private float originalNoiseScale = 0.2f;
    private int[] originalDenoisingSteps = null;
    private string originalGeminiSystemPrompt = "";
    private bool originalVisibleObjectsPrompt = false;
    
    // Current emotion and gesture for image generation
    private string currentEmotion = "neutral";
    private string currentGesture = "standing";
    private bool isTalking = false;
    
    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[CharacterChat] Collider on {gameObject.name} is not set to trigger. Setting it now.");
            col.isTrigger = true;
        }
        
        // Auto-find references if not assigned
        if (promptManager == null)
        {
            promptManager = FindFirstObjectByType<PromptManager>();
            if (promptManager == null)
            {
                Debug.LogWarning($"[CharacterChat] No PromptManager found in scene.");
            }
        }
        
        if (inputSwitcher == null)
        {
            inputSwitcher = FindFirstObjectByType<InputSwitcher>();
            if (inputSwitcher == null)
            {
                Debug.LogError($"[CharacterChat] No InputSwitcher found in scene! Required for chat mode control.");
            }
        }
        
        if (geminiChatManager == null)
        {
            geminiChatManager = FindFirstObjectByType<GeminiChatManager>();
            if (geminiChatManager == null)
            {
                Debug.LogError($"[CharacterChat] No GeminiChatManager found in scene! Required for chat functionality.");
            }
        }
        
        // Hide interaction prompt initially
        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false);
        }
        
        // Subscribe to Gemini events
        if (geminiChatManager != null)
        {
            geminiChatManager.OnTypingStarted += OnGeminiTypingStarted;
            geminiChatManager.OnTypingFinished += OnGeminiTypingFinished;
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (geminiChatManager != null)
        {
            geminiChatManager.OnTypingStarted -= OnGeminiTypingStarted;
            geminiChatManager.OnTypingFinished -= OnGeminiTypingFinished;
        }
    }

    private void Update()
    {
        // Only process input when player is inside the zone
        if (!isPlayerInside) return;
        
        if (Mouse.current != null)
        {
            // F key to enter chat mode
            if (Keyboard.current.fKey.wasPressedThisFrame && !isInChatMode)
            {
                EnterChatMode();
            }
        }
        
        if (Keyboard.current != null)
        {
            // Escape to exit chat mode
            if (Keyboard.current.escapeKey.wasPressedThisFrame && isInChatMode)
            {
                ExitChatMode();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        
        isPlayerInside = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Player entered {gameObject.name}");
        }
        
        // Show interaction prompt
        if (interactionPromptText != null)
        {
            interactionPromptText.text = interactionMessage;
            interactionPromptText.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        
        isPlayerInside = false;
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Player exited {gameObject.name}");
        }
        
        // Hide interaction prompt
        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false);
        }
        
        // Exit chat mode if still in it (this will also restore image generation settings)
        if (isInChatMode)
        {
            ExitChatMode();
        }
    }

    #endregion

    #region Gemini Event Handlers
    
    private void OnGeminiTypingStarted(string emotion, string gesture)
    {
        if (!isInChatMode) return;
        
        currentEmotion = emotion;
        currentGesture = gesture;
        isTalking = true;
        
        UpdateImagePromptWithState();
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] AI started talking - emotion: {emotion}, gesture: {gesture}");
        }
    }
    
    private void OnGeminiTypingFinished()
    {
        if (!isInChatMode) return;
        
        isTalking = false;
        
        UpdateImagePromptWithState();
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] AI finished talking");
        }
    }
    
    private void UpdateImagePromptWithState()
    {
        if (promptManager == null) return;
        
        // Build the prompt with emotion, gesture, and talking state
        string statePrompt = BuildStatePrompt();
        
        promptManager.SetPrefix(statePrompt);
        promptManager.SetAction("");
        promptManager.SetSuffix("");
        
        // Reset cache to apply changes immediately
        promptManager.ResetCache();
    }
    
    private string BuildStatePrompt()
    {
        // Start with the base replacement prompt
        string prompt = replacementPrompt;
        
        // Add emotion
        if (!string.IsNullOrEmpty(currentEmotion) && currentEmotion != "neutral")
        {
            prompt += $", {currentEmotion} expression";
        }
        
        // Add gesture
        if (!string.IsNullOrEmpty(currentGesture) && currentGesture != "standing")
        {
            // Convert underscores to spaces for better prompt formatting
            string gestureFormatted = currentGesture.Replace("_", " ");
            prompt += $", {gestureFormatted}";
        }
        
        // Add talking state
        if (isTalking)
        {
            prompt += ", talking";
        }
        else
        {
            prompt += ", not talking";
        }
        
        return prompt;
    }
    
    #endregion
    
    #region Chat Mode Management

    private void EnterChatMode()
    {
        if (inputSwitcher == null || geminiChatManager == null)
        {
            Debug.LogError("[CharacterChat] Cannot enter chat mode - missing references!");
            return;
        }
        
        isInChatMode = true;
        
        // Hide interaction prompt while in chat
        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false);
        }
        
        // Display character name in chat UI
        if (characterNameText != null)
        {
            characterNameText.text = characterName;
        }
        
        // Apply image generation prompt settings when entering chat
        ApplyImageGenerationSettings();
        
        // Reset cache to apply new settings immediately
        if (promptManager != null)
        {
            promptManager.ResetCache();
        }
        
        // Clear conversation history and start fresh
        geminiChatManager.ClearConversation();
        
        // Set custom system prompt for this character
        geminiChatManager.SetSystemPrompt(characterSystemPrompt);
        
        // Switch to chat UI mode
        inputSwitcher.SetChatMode(true);
        
        // Have the AI introduce itself
        geminiChatManager.SendGreeting();
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Entered chat mode with character: {gameObject.name}");
        }
    }

    private void ExitChatMode()
    {
        if (inputSwitcher == null)
        {
            Debug.LogError("[CharacterChat] Cannot exit chat mode - InputSwitcher reference missing!");
            return;
        }
        
        isInChatMode = false;
        
        // Restore image generation settings FIRST so the API starts processing immediately
        // This happens before the transition starts, giving maximum time for the AI to process
        if (restoreOnExit)
        {
            RestoreImageGenerationSettings();
        }
        
        // Now switch back to gameplay UI mode - transition will cover while AI processes
        inputSwitcher.SetChatMode(false);
        
        // Show interaction prompt again if player still inside
        if (isPlayerInside && interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(true);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Exited chat mode - prompt restored and sent to API before transition");
        }
    }

    #endregion

    #region Image Generation Settings

    private void ApplyImageGenerationSettings()
    {
        if (promptManager == null) return;
        
        // Store original values
        originalPrefix = promptManager.GetPrefix();
        originalSuffix = promptManager.GetSuffix();
        originalAutoUpdate = promptManager.GetAutoUpdate();
        originalNoiseScale = promptManager.GetNoiseScale();
        originalDenoisingSteps = promptManager.GetDenoisingSteps();
        originalVisibleObjectsPrompt = promptManager.GetVisibleObjectsPrompt();
        
        // Disable visible objects prompt during chat
        promptManager.SetVisibleObjectsPrompt(false);
        
        // Disable auto-update if requested
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(false);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Applying image generation settings - prompt: {replacementPrompt}, noise scale: {replacementNoiseScale}");
        }
        
        // Replace the entire prompt
        promptManager.SetPrefix(replacementPrompt);
        promptManager.SetAction("");
        promptManager.SetSuffix("");
        
        // Apply the replacement noise scale
        promptManager.SetNoiseScale(replacementNoiseScale);
        
        // Modify denoising step at index 2
        if (originalDenoisingSteps != null && originalDenoisingSteps.Length > 2)
        {
            int[] modifiedSteps = (int[])originalDenoisingSteps.Clone();
            modifiedSteps[2] = replacementDenoisingStep2;
            promptManager.SetDenoisingSteps(modifiedSteps);
        }
        
        // Reset cache with new settings
        promptManager.ResetCache();
        
        // Ensure auto-update stays disabled if requested
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(false);
        }
    }

    private void RestoreImageGenerationSettings()
    {
        if (promptManager == null) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Restoring original image generation settings");
        }
        
        // Restore original noise scale
        promptManager.SetNoiseScale(originalNoiseScale);
        
        // Restore original denoising steps
        if (originalDenoisingSteps != null)
        {
            promptManager.SetDenoisingSteps(originalDenoisingSteps);
        }
        
        // Restore to the default values from PromptManager
        promptManager.SetPrefix(promptManager.GetDefaultPrefix());
        promptManager.SetSuffix(promptManager.GetDefaultSuffix());
        
        // Restore auto-update state
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(originalAutoUpdate);
        }
        
        // Restore visible objects prompt state
        promptManager.SetVisibleObjectsPrompt(originalVisibleObjectsPrompt);
        
        // Reset cache first, then explicitly send the restored prompt to the API
        promptManager.ResetCache();
        promptManager.UpdatePrompt();
        
        if (showDebugLogs)
        {
            Debug.Log($"[CharacterChat] Sent restored prompt to API: prefix='{promptManager.GetDefaultPrefix()}'");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Check if the player is currently inside this zone.
    /// </summary>
    public bool IsPlayerInside() => isPlayerInside;

    /// <summary>
    /// Check if currently in chat mode with this character.
    /// </summary>
    public bool IsInChatMode() => isInChatMode;

    /// <summary>
    /// Manually set the character's system prompt at runtime.
    /// </summary>
    public void SetCharacterSystemPrompt(string newPrompt)
    {
        characterSystemPrompt = newPrompt;
        
        // If currently in chat mode, update immediately
        if (isInChatMode && geminiChatManager != null)
        {
            geminiChatManager.SetSystemPrompt(characterSystemPrompt);
        }
    }

    /// <summary>
    /// Manually set the replacement image generation prompt at runtime.
    /// </summary>
    public void SetReplacementPrompt(string newPrompt)
    {
        replacementPrompt = newPrompt;
        
        // If player is already inside, update immediately
        if (isPlayerInside && promptManager != null)
        {
            promptManager.SetPrefix(replacementPrompt);
            promptManager.SetSuffix("");
        }
    }

    #endregion

    #region Editor Visualization

    private void OnDrawGizmos()
    {
        // Draw a visual representation in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Use cyan color to distinguish from other trigger zones
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);
            }
        }
    }

    #endregion
}
