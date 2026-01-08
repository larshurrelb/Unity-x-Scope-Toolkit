using UnityEngine;

public class MinimapToggle : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera to use for the minimap (attached to main camera by default)")]
    [SerializeField] private Camera minimapCamera;
    
    [Tooltip("The player armature transform to position camera above")]
    [SerializeField] private Transform playerArmature;
    
    [Tooltip("The DepthColorToTexture component (will get material from it)")]
    [SerializeField] private DepthColorToTexture depthColorToTexture;
    
    [Tooltip("UI arrow element to show player direction in minimap mode")]
    [SerializeField] private RectTransform arrowUI;
    
    [Tooltip("OpenPoseSkeletonRenderer to disable when minimap is active")]
    [SerializeField] private OpenPoseSkeletonRenderer openPoseRenderer;
    
    [Header("Minimap Settings")]
    [Tooltip("Height above the player armature when in minimap mode")]
    [SerializeField] private float minimapHeight = 10f;
    
    [Tooltip("Camera rotation when in minimap mode (looking down)")]
    [SerializeField] private Vector3 minimapRotation = new Vector3(90f, 0f, 0f);
    
    [Header("Depth Settings")]
    [Tooltip("Depth min value for normal mode")]
    [SerializeField] private float normalDepthMin = 0f;
    
    [Tooltip("Depth max value for normal mode")]
    [SerializeField] private float normalDepthMax = 0.074f;
    
    [Tooltip("Depth min value for minimap mode")]
    [SerializeField] private float minimapDepthMin = 0.022f;
    
    [Tooltip("Depth max value for minimap mode")]
    [SerializeField] private float minimapDepthMax = 0.019f;
    
    [Header("Follow Settings")]
    [Tooltip("Smooth follow speed when in minimap mode")]
    [SerializeField] private float followSpeed = 10f;
    
    // Original camera state
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private float originalFieldOfView;
    private float originalNearClip;
    private float originalFarClip;
    
    // State
    private bool isMinimapMode = true;
    private bool originalStateStored = false;
    private bool openPoseWasEnabled = false;

    void Start()
    {
        // Validate references
        if (minimapCamera == null)
        {
            Debug.LogError("[MinimapToggle] Minimap camera not assigned!");
            enabled = false;
            return;
        }
        
        if (playerArmature == null)
        {
            Debug.LogError("[MinimapToggle] Player armature not assigned!");
            enabled = false;
            return;
        }
        
        if (depthColorToTexture == null)
        {
            Debug.LogError("[MinimapToggle] DepthColorToTexture component not assigned!");
            enabled = false;
            return;
        }
        
        // Get material from DepthColorToTexture
        Material depthMaterial = depthColorToTexture.DepthMaterial;
        if (depthMaterial == null)
        {
            Debug.LogError("[MinimapToggle] DepthColorToTexture has no material assigned!");
            enabled = false;
            return;
        }
        
        // Store original camera state BEFORE activating minimap
        StoreOriginalState();
        
        // Auto-find OpenPoseSkeletonRenderer if not assigned
        if (openPoseRenderer == null)
        {
            openPoseRenderer = FindFirstObjectByType<OpenPoseSkeletonRenderer>();
        }
        
        // Activate minimap mode on start
        ActivateMinimapMode();
        Debug.Log("[MinimapToggle] Minimap mode activated on start");
    }

    void LateUpdate()
    {
        // Follow player in minimap mode
        if (isMinimapMode && playerArmature != null)
        {
            // Calculate target position above player
            Vector3 targetPosition = playerArmature.position + Vector3.up * minimapHeight;
            
            // Smoothly move camera to target position
            minimapCamera.transform.position = Vector3.Lerp(
                minimapCamera.transform.position, 
                targetPosition, 
                Time.deltaTime * followSpeed
            );
            
            // Maintain fixed rotation (looking down)
            minimapCamera.transform.rotation = Quaternion.Euler(minimapRotation);
            
            // Rotate arrow UI to match player's Y-axis rotation
            if (arrowUI != null)
            {
                arrowUI.localRotation = Quaternion.Euler(0f, 0f, -playerArmature.eulerAngles.y);
            }
        }
    }

    /// <summary>
    /// Toggle between minimap mode and normal camera mode
    /// </summary>
    public void ToggleMinimapMode()
    {
        if (isMinimapMode)
        {
            // Switch back to normal camera mode
            RestoreOriginalState();
        }
        else
        {
            // Switch to minimap mode
            ActivateMinimapMode();
        }
        
        isMinimapMode = !isMinimapMode;
        Debug.Log($"[MinimapToggle] Minimap mode: {(isMinimapMode ? "ON" : "OFF")}");
    }

    /// <summary>
    /// Store the original camera state when scene starts
    /// </summary>
    private void StoreOriginalState()
    {
        if (minimapCamera == null) return;
        
        originalParent = minimapCamera.transform.parent;
        originalLocalPosition = minimapCamera.transform.localPosition;
        originalLocalRotation = minimapCamera.transform.localRotation;
        originalFieldOfView = minimapCamera.fieldOfView;
        originalNearClip = minimapCamera.nearClipPlane;
        originalFarClip = minimapCamera.farClipPlane;
        
        originalStateStored = true;
        Debug.Log("[MinimapToggle] Original camera state stored");
    }

    /// <summary>
    /// Activate minimap mode - position camera above player
    /// </summary>
    private void ActivateMinimapMode()
    {
        if (minimapCamera == null || playerArmature == null) return;
        
        // Detach from parent
        minimapCamera.transform.SetParent(null);
        
        // Position above player
        minimapCamera.transform.position = playerArmature.position + Vector3.up * minimapHeight;
        
        // Look down
        minimapCamera.transform.rotation = Quaternion.Euler(minimapRotation);
        
        // Update material depth values for minimap
        if (depthColorToTexture != null && depthColorToTexture.DepthMaterial != null)
        {
            Material depthMaterial = depthColorToTexture.DepthMaterial;
            depthMaterial.SetFloat("_DepthMin", minimapDepthMin);
            depthMaterial.SetFloat("_DepthMax", minimapDepthMax);
            
            float verifyMin = depthMaterial.GetFloat("_DepthMin");
            float verifyMax = depthMaterial.GetFloat("_DepthMax");
            Debug.Log($"[MinimapToggle] Set minimap depth - Target: Min={minimapDepthMin}, Max={minimapDepthMax}, Actual: Min={verifyMin}, Max={verifyMax}");
        }
        
        // Activate arrow UI
        if (arrowUI != null)
        {
            arrowUI.gameObject.SetActive(true);
        }
        
        // Store OpenPose state and disable it
        if (openPoseRenderer != null)
        {
            openPoseWasEnabled = openPoseRenderer.showOpenPose;
            openPoseRenderer.showOpenPose = false;
            Debug.Log("[MinimapToggle] OpenPose disabled for minimap mode");
        }
        
        Debug.Log("[MinimapToggle] Activated minimap mode");
    }

    /// <summary>
    /// Restore camera to original state (attached to main camera)
    /// </summary>
    private void RestoreOriginalState()
    {
        if (minimapCamera == null || !originalStateStored) return;
        
        // Re-attach to original parent
        minimapCamera.transform.SetParent(originalParent);
        
        // Restore original transform
        minimapCamera.transform.localPosition = originalLocalPosition;
        minimapCamera.transform.localRotation = originalLocalRotation;
        
        // Restore camera settings
        minimapCamera.fieldOfView = originalFieldOfView;
        minimapCamera.nearClipPlane = originalNearClip;
        minimapCamera.farClipPlane = originalFarClip;
        
        // Restore normal depth values
        if (depthColorToTexture != null && depthColorToTexture.DepthMaterial != null)
        {
            Material depthMaterial = depthColorToTexture.DepthMaterial;
            depthMaterial.SetFloat("_DepthMin", normalDepthMin);
            depthMaterial.SetFloat("_DepthMax", normalDepthMax);
            
            float verifyMin = depthMaterial.GetFloat("_DepthMin");
            float verifyMax = depthMaterial.GetFloat("_DepthMax");
            Debug.Log($"[MinimapToggle] Restored depth - Target: Min={normalDepthMin}, Max={normalDepthMax}, Actual: Min={verifyMin}, Max={verifyMax}");
        }
        
        // Deactivate arrow UI
        if (arrowUI != null)
        {
            arrowUI.gameObject.SetActive(false);
        }
        
        // Restore OpenPose state
        if (openPoseRenderer != null && openPoseWasEnabled)
        {
            openPoseRenderer.showOpenPose = true;
            Debug.Log("[MinimapToggle] OpenPose restored");
        }
        
        Debug.Log("[MinimapToggle] Restored original camera state");
    }

    /// <summary>
    /// Public getter for current state
    /// </summary>
    public bool IsMinimapMode => isMinimapMode;

    void OnDisable()
    {
        // Ensure camera is restored when disabled
        if (isMinimapMode)
        {
            RestoreOriginalState();
            isMinimapMode = false;
        }
    }
}
