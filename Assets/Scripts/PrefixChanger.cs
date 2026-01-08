using UnityEngine;

/// <summary>
/// Changes the prompt prefix when the player enters the trigger collider.
/// Attach this to a GameObject with a Collider set to "Is Trigger".
/// </summary>
[RequireComponent(typeof(Collider))]
public class PrefixChanger : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("The new prefix to set when player enters this area")]
    [TextArea(2, 5)]
    [SerializeField] private string newPrefix = "A futuristic cyberpunk scene with";
    
    [Tooltip("If true, adds this prefix on top of the existing one. If false, replaces it completely.")]
    [SerializeField] private bool appendToExisting = false;
    
    [Tooltip("Restore the original prefix when player exits")]
    [SerializeField] private bool restoreOnExit = true;
    
    [Header("Noise Scale Override")]
    [Tooltip("Override the noise scale while player is in this zone")]
    [SerializeField] private bool overrideNoiseScale = false;
    
    [Range(0f, 1f)]
    [Tooltip("The noise scale to use while in this zone (only applies if overrideNoiseScale is true)")]
    [SerializeField] private float noiseScaleOverride = 0.3f;
    
    [Header("Denoising Step Override")]
    [Tooltip("Override the second denoising step while player is in this zone")]
    [SerializeField] private bool overrideStep2 = false;
    
    [Tooltip("The value for denoising step 2 while in this zone (only applies if overrideStep2 is true)")]
    [SerializeField] private int step2Override = 750;
    
    [Header("Detection Settings")]
    [Tooltip("Tag to detect for player (default: Player)")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("References")]
    [Tooltip("Reference to the PromptManager (auto-finds if not set)")]
    [SerializeField] private PromptManager promptManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool isPlayerInside = false;
    private float originalNoiseScale = 0f;
    private int[] originalDenoisingSteps = null;

    private void Start()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[PrefixChanger] Collider on {gameObject.name} is not set to trigger. Setting it now.");
            col.isTrigger = true;
        }
        
        // Auto-find PromptManager if not assigned
        if (promptManager == null)
        {
            promptManager = FindObjectOfType<PromptManager>();
            if (promptManager == null)
            {
                Debug.LogError($"[PrefixChanger] No PromptManager found in scene! Attach PromptManager to use this script.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the colliding object is the player
        if (!other.CompareTag(playerTag)) return;
        
        if (promptManager == null)
        {
            Debug.LogError("[PrefixChanger] PromptManager reference is null!");
            return;
        }
        
        isPlayerInside = true;
        
        string finalPrefix;
        
        if (appendToExisting)
        {
            // Append to the default prefix
            string defaultPrefix = promptManager.GetDefaultPrefix();
            finalPrefix = $"{defaultPrefix} {newPrefix}".Trim();
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Player entered {gameObject.name}, appending to prefix: {finalPrefix}");
            }
        }
        else
        {
            // Replace the prefix entirely
            finalPrefix = newPrefix;
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Player entered {gameObject.name}, replacing prefix with: {finalPrefix}");
            }
        }
        
        // Update the prefix
        promptManager.SetPrefix(finalPrefix);
        
        // Apply noise scale override if enabled
        if (overrideNoiseScale)
        {
            originalNoiseScale = promptManager.GetNoiseScale();
            promptManager.SetExternalNoiseOverride(noiseScaleOverride);
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Overriding noise scale to: {noiseScaleOverride} (was: {originalNoiseScale})");
            }
        }
        
        // Apply step 2 override if enabled
        if (overrideStep2)
        {
            originalDenoisingSteps = promptManager.GetDenoisingSteps();
            if (originalDenoisingSteps != null && originalDenoisingSteps.Length > 1)
            {
                int[] modifiedSteps = (int[])originalDenoisingSteps.Clone();
                modifiedSteps[1] = step2Override;
                promptManager.SetDenoisingSteps(modifiedSteps);
                
                if (showDebugLogs)
                {
                    Debug.Log($"[PrefixChanger] Overriding step 2 to: {step2Override} (was: {originalDenoisingSteps[1]})");
                }
            }
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
            string defaultPrefix = promptManager.GetDefaultPrefix();
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Player exited {gameObject.name}, restoring to default prefix: {defaultPrefix}");
            }
            
            // Restore to the default prefix from PromptManager
            promptManager.SetPrefix(defaultPrefix);
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[PrefixChanger] Player exited {gameObject.name}.");
        }
        
        // Restore noise scale if it was overridden
        if (overrideNoiseScale)
        {
            promptManager.ClearExternalNoiseOverride();
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Clearing noise scale override");
            }
        }
        
        // Restore step 2 if it was overridden
        if (overrideStep2 && originalDenoisingSteps != null)
        {
            promptManager.SetDenoisingSteps(originalDenoisingSteps);
            
            if (showDebugLogs)
            {
                Debug.Log($"[PrefixChanger] Restoring original denoising steps");
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Draw a visual representation in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.3f);
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
}
