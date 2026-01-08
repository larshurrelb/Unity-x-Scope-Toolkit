using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Provides manual API key input UI for RunPod URL and Gemini API key.
/// When this GameObject is active, users can enter custom API credentials.
/// When inactive, the default inspector values on the managers are used.
/// </summary>
public class ManualAPIKeyInput : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Input field for the RunPod URL")]
    [SerializeField] private TMP_InputField runPodUrlInput;
    
    [Tooltip("Input field for the Gemini API key")]
    [SerializeField] private TMP_InputField geminiApiKeyInput;
    
    [Tooltip("Button to confirm and apply the API settings")]
    [SerializeField] private Button confirmButton;
    
    [Header("Manager References")]
    [Tooltip("Reference to the DaydreamAPIManager (auto-finds if not assigned)")]
    [SerializeField] private DaydreamAPIManager daydreamAPIManager;
    
    [Tooltip("Reference to the GeminiChatManager (auto-finds if not assigned)")]
    [SerializeField] private GeminiChatManager geminiChatManager;
    
    [Header("Settings")]
    [Tooltip("Hide this UI after confirming")]
    [SerializeField] private bool hideOnConfirm = true;
    
    [Tooltip("Optional: GameObject to activate after confirming (e.g., start screen)")]
    [SerializeField] private GameObject activateOnConfirm;

    private void Start()
    {
        // Auto-find managers if not assigned
        if (daydreamAPIManager == null)
        {
            daydreamAPIManager = FindFirstObjectByType<DaydreamAPIManager>();
            if (daydreamAPIManager == null)
            {
                Debug.LogWarning("[ManualAPIKeyInput] No DaydreamAPIManager found in scene!");
            }
        }
        
        if (geminiChatManager == null)
        {
            geminiChatManager = FindFirstObjectByType<GeminiChatManager>();
            if (geminiChatManager == null)
            {
                Debug.LogWarning("[ManualAPIKeyInput] No GeminiChatManager found in scene!");
            }
        }
        
        // Setup confirm button listener
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        else
        {
            Debug.LogWarning("[ManualAPIKeyInput] Confirm button not assigned!");
        }
        
        // Set placeholder text with current values if available
        if (runPodUrlInput != null && daydreamAPIManager != null)
        {
            string currentUrl = daydreamAPIManager.GetRunPodUrl();
            if (!string.IsNullOrEmpty(currentUrl))
            {
                runPodUrlInput.text = currentUrl;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }
    
    /// <summary>
    /// Called when the confirm button is clicked.
    /// Applies the entered API credentials to the managers.
    /// </summary>
    private void OnConfirmClicked()
    {
        bool success = true;
        
        // Apply RunPod URL
        if (daydreamAPIManager != null && runPodUrlInput != null)
        {
            string url = runPodUrlInput.text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                daydreamAPIManager.SetRunPodUrl(url);
                Debug.Log($"[ManualAPIKeyInput] RunPod URL set to: {url}");
            }
            else
            {
                Debug.LogWarning("[ManualAPIKeyInput] RunPod URL is empty, using default.");
            }
        }
        else
        {
            if (daydreamAPIManager == null)
            {
                Debug.LogError("[ManualAPIKeyInput] Cannot set RunPod URL - DaydreamAPIManager not found!");
                success = false;
            }
        }
        
        // Apply Gemini API Key
        if (geminiChatManager != null && geminiApiKeyInput != null)
        {
            string apiKey = geminiApiKeyInput.text.Trim();
            if (!string.IsNullOrEmpty(apiKey))
            {
                geminiChatManager.SetApiKey(apiKey);
                Debug.Log("[ManualAPIKeyInput] Gemini API key set successfully.");
            }
            else
            {
                Debug.LogWarning("[ManualAPIKeyInput] Gemini API key is empty, using default.");
            }
        }
        else
        {
            if (geminiChatManager == null)
            {
                Debug.LogError("[ManualAPIKeyInput] Cannot set Gemini API key - GeminiChatManager not found!");
                success = false;
            }
        }
        
        if (success)
        {
            Debug.Log("[ManualAPIKeyInput] API credentials applied successfully!");
            
            // Activate the next UI if specified
            if (activateOnConfirm != null)
            {
                activateOnConfirm.SetActive(true);
            }
            
            // Hide this UI if configured
            if (hideOnConfirm)
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Programmatically set the RunPod URL input field value.
    /// </summary>
    public void SetRunPodUrlInputText(string url)
    {
        if (runPodUrlInput != null)
        {
            runPodUrlInput.text = url;
        }
    }
    
    /// <summary>
    /// Programmatically set the Gemini API key input field value.
    /// </summary>
    public void SetGeminiApiKeyInputText(string apiKey)
    {
        if (geminiApiKeyInput != null)
        {
            geminiApiKeyInput.text = apiKey;
        }
    }
    
    /// <summary>
    /// Trigger the confirm action programmatically.
    /// </summary>
    public void Confirm()
    {
        OnConfirmClicked();
    }
}
