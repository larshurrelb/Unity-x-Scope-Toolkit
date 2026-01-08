using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rotates a UI image to indicate the camera's angle relative to the player from a top-down view.
/// The indicator shows which direction the camera is looking at the player from.
/// </summary>
public class CameraDirectionIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RectTransform of the UI image to rotate")]
    [SerializeField] private RectTransform indicatorImage;
    
    [Tooltip("The player transform (auto-finds if not set)")]
    [SerializeField] private Transform playerTransform;
    
    [Tooltip("The camera transform (auto-finds MainCamera if not set)")]
    [SerializeField] private Transform cameraTransform;
    
    [Header("Settings")]
    [Tooltip("Offset angle in degrees (0 = indicator points up when camera is behind player)")]
    [SerializeField] private float angleOffset = 0f;
    
    [Tooltip("Invert the rotation direction")]
    [SerializeField] private bool invertRotation = false;
    
    [Tooltip("Smooth rotation speed (0 = instant, higher = slower)")]
    [SerializeField] private float smoothSpeed = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private float targetRotation = 0f;

    private void Start()
    {
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("[CameraDirectionIndicator] No player transform assigned and couldn't find object with 'Player' tag!");
            }
        }
        
        // Auto-find camera if not assigned
        if (cameraTransform == null)
        {
            GameObject mainCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
            else
            {
                Debug.LogError("[CameraDirectionIndicator] No camera transform assigned and couldn't find MainCamera!");
            }
        }
        
        // Validate indicator image
        if (indicatorImage == null)
        {
            Debug.LogError("[CameraDirectionIndicator] No indicator image RectTransform assigned!");
        }
    }

    private void LateUpdate()
    {
        if (indicatorImage == null || playerTransform == null || cameraTransform == null)
            return;
        
        // Calculate the direction from player to camera (on XZ plane)
        Vector3 playerPosition = playerTransform.position;
        Vector3 cameraPosition = cameraTransform.position;
        
        // Flatten to XZ plane (top-down view)
        Vector3 directionToCamera = new Vector3(
            cameraPosition.x - playerPosition.x,
            0f,
            cameraPosition.z - playerPosition.z
        );
        
        // Calculate angle in degrees
        // Atan2 returns angle from positive X axis, we convert to UI rotation
        // In Unity UI, 0 degrees is right, 90 is up, 180 is left, 270 is down
        // We want to show where the camera is relative to the player's forward direction
        float angle = Mathf.Atan2(directionToCamera.x, directionToCamera.z) * Mathf.Rad2Deg;
        
        // Apply offset
        angle += angleOffset;
        
        // Invert if needed
        if (invertRotation)
        {
            angle = -angle;
        }
        
        targetRotation = angle;
        
        // Apply rotation with optional smoothing
        if (smoothSpeed > 0f)
        {
            float currentZ = indicatorImage.localEulerAngles.z;
            float smoothedAngle = Mathf.LerpAngle(currentZ, targetRotation, Time.deltaTime * smoothSpeed);
            indicatorImage.localEulerAngles = new Vector3(0f, 0f, smoothedAngle);
        }
        else
        {
            indicatorImage.localEulerAngles = new Vector3(0f, 0f, targetRotation);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[CameraDirectionIndicator] Camera angle: {angle:F1}°");
        }
    }
}
