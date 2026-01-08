using UnityEngine;

/// <summary>
/// Replaces the entire prompt (prefix + action + suffix) when the player enters the trigger collider.
/// Reverts to the default prompt when the player exits.
/// Attach this to a GameObject with a Collider set to "Is Trigger".
/// </summary>
[RequireComponent(typeof(Collider))]
public class PromptReplacer : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("The complete prompt to use when player is inside this area")]
    [TextArea(3, 10)]
    [SerializeField] private string replacementPrompt = "A cyberpunk city scene with neon lights and flying cars";
    
    [Header("Noise Settings")]
    [Range(0f, 1f)]
    [Tooltip("Noise scale to use when player is inside this area (0.0-1.0). Higher = more variation, lower = more faithful to input")]
    [SerializeField] private float replacementNoiseScale = 0.5f;
    
    [Header("Denoising Settings")]
    [Tooltip("Override the second denoising step (index 1) while player is in this zone")]
    [SerializeField] private bool overrideStep2 = false;
    
    [Tooltip("The value for denoising step 2 (index 1) while in this zone")]
    [SerializeField] private int step2Override = 750;
    
    [Tooltip("Replacement value for denoising step at index 2 (default is 500, change to 900 for different effect)")]
    [SerializeField] private int replacementDenoisingStep2 = 900;
    
    [Header("Behavior")]
    [Tooltip("Disable auto-update while player is in zone (prevents action changes)")]
    [SerializeField] private bool disableAutoUpdate = true;
    
    [Tooltip("Restore defaults when player exits")]
    [SerializeField] private bool restoreOnExit = true;
    
    [Header("Detection Settings")]
    [Tooltip("Tag to detect for player (default: Player)")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("References")]
    [Tooltip("Reference to the PromptManager (auto-finds if not set)")]
    [SerializeField] private PromptManager promptManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool isPlayerInside = false;
    private string originalPrefix = "";
    private string originalAction = "";
    private string originalSuffix = "";
    private bool originalAutoUpdate = true;
    private float originalNoiseScale = 0.2f;
    private int[] originalDenoisingSteps = null;

    private void Start()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[PromptReplacer] Collider on {gameObject.name} is not set to trigger. Setting it now.");
            col.isTrigger = true;
        }
        
        // Auto-find PromptManager if not assigned
        if (promptManager == null)
        {
            promptManager = FindObjectOfType<PromptManager>();
            if (promptManager == null)
            {
                Debug.LogError($"[PromptReplacer] No PromptManager found in scene! Attach PromptManager to use this script.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the colliding object is the player
        if (!other.CompareTag(playerTag)) return;
        
        if (promptManager == null)
        {
            Debug.LogError("[PromptReplacer] PromptManager reference is null!");
            return;
        }
        
        isPlayerInside = true;
        
        // Store original values
        originalPrefix = promptManager.GetPrefix();
        originalSuffix = promptManager.GetSuffix();
        originalAutoUpdate = promptManager.GetAutoUpdate();
        originalNoiseScale = promptManager.GetNoiseScale();
        originalDenoisingSteps = promptManager.GetDenoisingSteps();
        
        // Disable auto-update if requested
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(false);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[PromptReplacer] Player entered {gameObject.name}, setting prompt to: {replacementPrompt}, noise scale: {replacementNoiseScale}");
        }
        
        // Replace the entire prompt by setting prefix to full prompt and clearing suffix and action
        promptManager.SetPrefix(replacementPrompt);
        promptManager.SetAction("");
        promptManager.SetSuffix("");
        
        // Apply the replacement noise scale
        promptManager.SetNoiseScale(replacementNoiseScale);
        
        // Modify denoising steps
        if (originalDenoisingSteps != null && originalDenoisingSteps.Length > 2)
        {
            int[] modifiedSteps = (int[])originalDenoisingSteps.Clone();
            
            // Override step 2 (index 1) if enabled
            if (overrideStep2 && modifiedSteps.Length > 1)
            {
                modifiedSteps[1] = step2Override;
                if (showDebugLogs)
                {
                    Debug.Log($"[PromptReplacer] Overriding step 2 to: {step2Override} (was: {originalDenoisingSteps[1]})");
                }
            }
            
            // Always apply step at index 2
            modifiedSteps[2] = replacementDenoisingStep2;
            promptManager.SetDenoisingSteps(modifiedSteps);
        }
        
        // Reset cache with new settings
        promptManager.ResetCache();
        
        // Disable auto-update if requested
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(false);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is the player
        if (!other.CompareTag(playerTag)) return;
        
        if (promptManager == null) return;
        
        isPlayerInside = false;
        
        if (restoreOnExit)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PromptReplacer] Player exited {gameObject.name}, restoring defaults");
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
        }
        
        // Restore auto-update state
        if (disableAutoUpdate)
        {
            promptManager.SetAutoUpdate(originalAutoUpdate);
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[PromptReplacer] Player exited {gameObject.name}.");
        }
        promptManager.ResetCache();
    }

    private void OnDrawGizmos()
    {
        // Draw a visual representation in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Use a different color to distinguish from SuffixChanger (purple/magenta)
            Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.3f);
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

    /// <summary>
    /// Check if the player is currently inside this zone.
    /// </summary>
    public bool IsPlayerInside()
    {
        return isPlayerInside;
    }

    /// <summary>
    /// Manually set the replacement prompt at runtime.
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
}
