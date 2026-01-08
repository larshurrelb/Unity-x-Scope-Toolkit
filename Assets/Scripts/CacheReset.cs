using UnityEngine;

/// <summary>
/// Collision trigger area that resets the LongLive API cache when the player enters.
/// Attach to a GameObject with a Collider component set as a trigger.
/// </summary>
public class CacheReset : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the PromptManager (auto-finds if not set)")]
    [SerializeField] private PromptManager promptManager;
    
    [Header("Settings")]
    [Tooltip("If true, only resets cache once per game session")]
    [SerializeField] private bool resetOnce = true; // If true, only resets cache once
    [Tooltip("Tag to detect for player (default: Player)")]
    [SerializeField] private string playerTag = "Player"; // Tag to check for player
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool hasReset = false;
    
    void Start()
    {
        // Auto-find PromptManager if not assigned
        if (promptManager == null)
        {
            promptManager = FindObjectOfType<PromptManager>();
            if (promptManager == null)
            {
                Debug.LogError("[CacheReset] No PromptManager found in scene! Please add a PromptManager.");
            }
        }
        
        // Check for collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[CacheReset] No Collider component found! Please add a Collider and set it as a trigger.");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("[CacheReset] Collider is not set as a trigger. Setting isTrigger to true.");
            col.isTrigger = true;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if already reset (if resetOnce is enabled)
        if (resetOnce && hasReset)
        {
            return;
        }
        
        // Check if the colliding object is the player
        if (other.CompareTag(playerTag))
        {
            ResetCache();
        }
    }
    
    /// <summary>
    /// Reset the cache via the PromptManager.
    /// </summary>
    private void ResetCache()
    {
        if (promptManager == null)
        {
            Debug.LogError("[CacheReset] Cannot reset cache - PromptManager reference is null!");
            return;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[CacheReset] Player entered {gameObject.name}, resetting cache...");
        }
        
        promptManager.ResetCache();
        
        if (resetOnce)
        {
            hasReset = true;
            if (showDebugLogs)
            {
                Debug.Log("[CacheReset] Cache reset completed. This trigger will not reset again.");
            }
        }
    }
    
    /// <summary>
    /// Manually reset the cache (can be called from other scripts or Unity Events).
    /// </summary>
    public void ManualReset()
    {
        hasReset = false;
        ResetCache();
    }

    private void OnDrawGizmos()
    {
        // Draw a visual representation in the editor
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.3f);
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
