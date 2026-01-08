using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using StarterAssets;

public class InputSwitcher : MonoBehaviour
{
    [Header("Video Texture Sources")]
    [SerializeField] private RenderTexture videoTexture1;
    [SerializeField] private RenderTexture videoTexture2; // Color depth cam (DepthColorToTexture)
    [SerializeField] private RenderTexture videoTexture3; // Removed - old depth cam no longer used
    
    [Header("Output Texture")]
    [SerializeField] private RenderTexture outputTexture;
    
    [Header("API Manager")]
    [SerializeField] private DaydreamAPIManager apiManager;
    
    [Header("UI Mode Switching")]
    [Tooltip("The start screen UI to show at game launch")]
    [SerializeField] private GameObject startScreenUI;
    [Tooltip("The gameplay UI to show during normal gameplay")]
    [SerializeField] private GameObject gameplayUI;
    [Tooltip("The chatbot UI to show when in chat mode")]
    [SerializeField] private GameObject chatbotUI;
    [Tooltip("Optional: Player controller to disable during chat mode and start screen")]
    [SerializeField] private ThirdPersonController playerController;
    [Tooltip("Optional: StarterAssetsInputs to disable during chat mode and start screen")]
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [Tooltip("Optional: PromptManager to take over after start screen")]
    [SerializeField] private PromptManager promptManager;
    [SerializeField] private GameObject BlackScreen;
    
    
    [Header("Start Screen Settings")]
    [Tooltip("Prompt to display on start screen")]
    [SerializeField] private string startScreenPrompt = "A cyberpunk start screen";
    [Tooltip("Skip the start screen and go directly to gameplay")]
    [SerializeField] private bool skipStartMenu = false;
    
    [Header("Transition Screen")]
    [Tooltip("Transition screen GameObject with animation that plays when switching UIs")]
    [SerializeField] private GameObject transitionScreen;
    
    [Header("Minimap")]
    [Tooltip("MinimapToggle component to control minimap mode")]
    [SerializeField] private MinimapToggle minimapToggle;
    
    [Header("OpenPose Skeleton")]
    [Tooltip("OpenPoseSkeletonRenderer component to control skeleton visualization")]
    [SerializeField] private OpenPoseSkeletonRenderer openPoseRenderer;
    
    private bool isChatMode = false;
    private bool isStartScreen = true;
    
    private int currentIndex = 1;
    private RenderTexture activeSourceTexture;
    [SerializeField] private GameObject Indicator; // Optional material to visualize the output texture
    [SerializeField] private GameObject parameterUI; // Parameter UI that includes video input and other controls
    

    

    void Update()
    {
        // Check for keyboard input using the new Input System
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                // Key 1 toggles between texture 1 and 2
                if (currentIndex == 1)
                {
                    SwitchToTexture(2);
                    SetActiveIndicator(1);
                }
                else
                {
                    SwitchToTexture(1);
                    SetActiveIndicator(0);
                }
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                // Key 2 toggles minimap mode
                if (minimapToggle != null)
                {
                    minimapToggle.ToggleMinimapMode();
                }
                else
                {
                    Debug.LogWarning("[InputSwitcher] MinimapToggle not assigned!");
                }
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                // Key 3 toggles OpenPose skeleton
                if (openPoseRenderer != null)
                {
                    openPoseRenderer.showOpenPose = !openPoseRenderer.showOpenPose;
                    Debug.Log($"[InputSwitcher] OpenPose skeleton: {(openPoseRenderer.showOpenPose ? "ON" : "OFF")}");
                }
                else
                {
                    Debug.LogWarning("[InputSwitcher] OpenPoseSkeletonRenderer not assigned!");
                }
            }
            else if (Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                // Key 0 resets API cache
                if (apiManager != null)
                {
                    apiManager.ResetCache();
                    Debug.Log("[InputSwitcher] Cache reset via keyboard input");
                }
                else
                {
                    Debug.LogWarning("[InputSwitcher] DaydreamAPIManager not assigned!");
                }
            }

            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                if (parameterUI != null)
                {
                    parameterUI.SetActive(!parameterUI.activeSelf);
                    Debug.Log($"[InputSwitcher] Toggled Parameter UI: {(parameterUI.activeSelf ? "ON" : "OFF")}");
                }
                else
                {
                    Debug.LogWarning("[InputSwitcher] Parameter UI not assigned!");
                }
            }
            
            // Space bar starts the game from start screen
            if (Keyboard.current.spaceKey.wasPressedThisFrame && isStartScreen)
            {
                StartGame();
            }
            
            // Note: Chat mode is now controlled by CharacterChat script via left mouse click
            // Escape key exits chat mode (handled here as a fallback)
            if (Keyboard.current.escapeKey.wasPressedThisFrame && isChatMode)
            {
                SetChatMode(false);
            }
        }

        // Continuously copy the active source texture to the output texture
        if (activeSourceTexture != null && outputTexture != null)
        {
            Graphics.CopyTexture(activeSourceTexture, outputTexture);
        }
    }
    // Helper to activate only the selected indicator child
    private void SetActiveIndicator(int activeIndex)
    {
        if (Indicator == null) return;
        int count = Indicator.transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Indicator.transform.GetChild(i).gameObject.SetActive(i == activeIndex);
        }
    }
    
    private void SwitchToTexture(int index)
    {
        if (outputTexture == null)
        {
            Debug.LogWarning("Output texture is not assigned!");
            return;
        }
        
        RenderTexture sourceTexture = null;
        
        switch (index)
        {
            case 1:
                sourceTexture = videoTexture1;
                break;
            case 2:
                sourceTexture = videoTexture2;
                break;
            case 3:
                sourceTexture = videoTexture3;
                break;
        }
        
        if (sourceTexture == null)
        {
            Debug.LogWarning($"Video texture {index} is not assigned!");
           
        if (apiManager == null)
        {
            apiManager = FindFirstObjectByType<DaydreamAPIManager>();
            if (apiManager == null)
            {
                Debug.LogWarning("[InputSwitcher] No DaydreamAPIManager found in scene!");
            }
        }
        
        //  return;
        }
        
        // Set the active source texture
        activeSourceTexture = sourceTexture;
        currentIndex = index;
        
        // Update skeleton renderer's source texture to match
        if (openPoseRenderer != null)
        {
            openPoseRenderer.sourceVideoTexture = sourceTexture;
            Debug.Log($"[InputSwitcher] Updated skeleton source texture to match input {index}");
        }
        
        Debug.Log($"Switched to video texture {index}");
    }
    
    void Start()
    {
        // Initialize by copying the first texture and setting the first indicator
        if (videoTexture1 != null && outputTexture != null)
        {
            SwitchToTexture(1);
        }
        SetActiveIndicator(0);
        
        // Hide parameter UI at start
        if (parameterUI != null)
        {
            parameterUI.SetActive(false);
        }
        
        // Find PromptManager if not assigned
        if (promptManager == null)
        {
            promptManager = FindFirstObjectByType<PromptManager>();
        }
        
        // Find API Manager if not assigned
        if (apiManager == null)
        {
            apiManager = FindFirstObjectByType<DaydreamAPIManager>();
        }
        
        // Find OpenPoseSkeletonRenderer if not assigned
        if (openPoseRenderer == null)
        {
            openPoseRenderer = FindFirstObjectByType<OpenPoseSkeletonRenderer>();
        }
        
        // Enable OpenPose by default
        if (openPoseRenderer != null)
        {
            openPoseRenderer.showOpenPose = true;
            Debug.Log("[InputSwitcher] OpenPose skeleton enabled by default");
        }
        
        // Check if we should skip the start menu
        if (skipStartMenu)
        {
            // Skip start screen and go directly to gameplay
            isStartScreen = false;
            BlackScreen.SetActive(false);
            
            // Initialize gameplay mode directly (no transition animation)
            StartCoroutine(SetStartScreenMode(false, false));
            
            Debug.Log("[InputSwitcher] Skipped start screen - starting directly in gameplay mode");
        }
        else
        {
            // Queue start screen parameters BEFORE data channel opens
            QueueStartScreenParameters();
            
            // Initialize UI mode (start with start screen, no transition animation)
            StartCoroutine(SetStartScreenMode(true, false));
        }
    }
    
    /// <summary>
    /// Toggle between gameplay and chat mode
    /// </summary>
    public void ToggleChatMode()
    {
        StartCoroutine(SetChatModeCoroutine(!isChatMode));
    }
    
    /// <summary>
    /// Set the chat mode state (public wrapper)
    /// </summary>
    /// <param name="chatMode">True for chat mode, false for gameplay mode</param>
    public void SetChatMode(bool chatMode)
    {
        StartCoroutine(SetChatModeCoroutine(chatMode));
    }
    
    /// <summary>
    /// Set the chat mode state (coroutine implementation)
    /// </summary>
    /// <param name="chatMode">True for chat mode, false for gameplay mode</param>
    private System.Collections.IEnumerator SetChatModeCoroutine(bool chatMode)
    {
        // Play transition animation
        if (transitionScreen != null)
        {
            transitionScreen.SetActive(true);
        }
        
        // Wait 1 second for transition animation to cover screen
        yield return new WaitForSeconds(1f);
        
        isChatMode = chatMode;
        
        // Toggle UI visibility
        if (gameplayUI != null)
        {
            gameplayUI.SetActive(!isChatMode);
        }
        
        if (chatbotUI != null)
        {
            chatbotUI.SetActive(isChatMode);
        }
        
        // Handle cursor for UI interaction
        if (isChatMode)
        {
            // Show and unlock cursor for chat UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Lock and hide cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        // Disable player controller during chat mode
        if (playerController != null)
        {
            playerController.enabled = !isChatMode;
        }
        
        // Disable input during chat mode
        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.cursorLocked = !isChatMode;
            starterAssetsInputs.cursorInputForLook = !isChatMode;
        }
        
        Debug.Log($"[InputSwitcher] Switched to {(isChatMode ? "Chat" : "Gameplay")} mode");
    }
    
    /// <summary>
    /// Check if currently in chat mode
    /// </summary>
    public bool IsChatMode => isChatMode;
    
    /// <summary>
    /// Check if currently on start screen
    /// </summary>
    public bool IsStartScreen => isStartScreen;
    
    /// <summary>
    /// Set start screen mode
    /// </summary>
    /// <param name="startScreenMode">True for start screen, false for normal mode</param>
    /// <param name="playTransition">Whether to play the transition animation (false on initial setup)</param>
    private System.Collections.IEnumerator SetStartScreenMode(bool startScreenMode, bool playTransition = true)
    {
        // Play transition animation only when switching, not on initial setup
        if (playTransition && transitionScreen != null)
        {
            transitionScreen.SetActive(true);
            // Wait 1 second for transition animation to cover screen
            yield return new WaitForSeconds(1f);
        }
        
        isStartScreen = startScreenMode;
        
        // Toggle UI visibility
        if (startScreenUI != null)
        {
            startScreenUI.SetActive(isStartScreen);
        }
        
        if (gameplayUI != null)
        {
            gameplayUI.SetActive(!isStartScreen && !isChatMode);
        }
        
        if (chatbotUI != null)
        {
            chatbotUI.SetActive(false); // Chat is never active on start screen
        }
        
        // Handle cursor and player controls
        if (isStartScreen)
        {
            // Show and unlock cursor for start screen
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Disable player controller on start screen
            if (playerController != null)
            {
                playerController.enabled = false;
            }
            
            // Disable input on start screen
            if (starterAssetsInputs != null)
            {
                starterAssetsInputs.cursorLocked = false;
                starterAssetsInputs.cursorInputForLook = false;
            }
            
            // Disable PromptManager on start screen
            if (promptManager != null)
            {
                promptManager.enabled = false;
            }
            
            // Send start screen parameters to API
            SendStartScreenParameters();
        }
        else
        {
            // Lock and hide cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Enable player controller
            if (playerController != null)
            {
                playerController.enabled = true;
            }
            
            // Enable input
            if (starterAssetsInputs != null)
            {
                starterAssetsInputs.cursorLocked = true;
                starterAssetsInputs.cursorInputForLook = true;
            }
            
            // Enable PromptManager to take over
            if (promptManager != null)
            {
                promptManager.enabled = true;
            }
        }
        
        Debug.Log($"[InputSwitcher] Start screen mode: {isStartScreen}");
    }
    
    /// <summary>
    /// Queue start screen parameters to be sent when data channel opens
    /// </summary>
    private void QueueStartScreenParameters()
    {
        if (apiManager == null)
        {
            Debug.LogWarning("[InputSwitcher] API Manager not assigned for start screen!");
            return;
        }
        
        // Create custom parameters for start screen
        var startParams = new DaydreamAPIManager.RunPodParameters
        {
            input_mode = "video",
            prompts = new System.Collections.Generic.List<DaydreamAPIManager.PromptData>
            {
                new DaydreamAPIManager.PromptData
                {
                    text = startScreenPrompt,
                    weight = 1.0f
                }
            },
            prompt_interpolation_method = "slerp",
            denoising_step_list = new int[] { 1000, 990 },
            noise_scale = 1.0f,
            manage_cache = true
        };
        
        // Queue parameters to be sent when data channel opens
        apiManager.SetInitialParameters(startParams);
        
        Debug.Log($"[InputSwitcher] Queued start screen parameters: prompt='{startScreenPrompt}', noise_scale=1.0, steps=[990]");
    }
    
    /// <summary>
    /// Send start screen parameters to API with noise scale 1.0 and denoising steps 990
    /// </summary>
    private void SendStartScreenParameters()
    {
        if (apiManager == null)
        {
            Debug.LogWarning("[InputSwitcher] API Manager not assigned for start screen!");
            return;
        }
        
        // Create custom parameters for start screen
        var startParams = new DaydreamAPIManager.RunPodParameters
        {
            input_mode = "video",
            prompts = new System.Collections.Generic.List<DaydreamAPIManager.PromptData>
            {
                new DaydreamAPIManager.PromptData
                {
                    text = startScreenPrompt,
                    weight = 1.0f
                }
            },
            prompt_interpolation_method = "slerp",
            denoising_step_list = new int[] { 990 },
            noise_scale = 1.0f,
            manage_cache = true
        };
        
        // Send parameters via API manager
        apiManager.SendCustomParameters(startParams);
        
        Debug.Log($"[InputSwitcher] Sent start screen parameters: prompt='{startScreenPrompt}', noise_scale=1.0, steps=[990]");
    }
    
    /// <summary>
    /// Start the game - transition from start screen to gameplay
    /// </summary>
    private void StartGame()
    {
        if (!isStartScreen)
        {
            Debug.LogWarning("[InputSwitcher] Already started game!");
            return;
        }
        
        Debug.Log("[InputSwitcher] Starting game...");
        
        // Reset cache as requested
        if (apiManager != null)
        {
            apiManager.ResetCache();
            Debug.Log("[InputSwitcher] Cache reset for game start");
        }
        
        // Switch to gameplay mode
        StartCoroutine(SetStartScreenMode(false));
        
        Debug.Log("[InputSwitcher] Game started - PromptManager now controlling prompts");
    }
}
