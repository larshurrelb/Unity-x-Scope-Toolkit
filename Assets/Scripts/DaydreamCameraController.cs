using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif
using StarterAssets;

/// <summary>
/// Custom camera controller for Daydream Live that extends the ThirdPersonController's camera behavior.
/// Add this component alongside ThirdPersonController to enable additional camera features:
/// - Maximum rotation speed limiting
/// - Camera pitch locking (prevent up/down movement)
/// - Keyboard-based camera rotation (Q/E keys)
/// 
/// This script automatically sets ThirdPersonController.LockCameraPosition = true and takes over camera rotation.
/// </summary>
[DefaultExecutionOrder(100)] // Run after ThirdPersonController to override camera rotation
public class DaydreamCameraController : MonoBehaviour
{
    [Header("Camera Target")]
    [Tooltip("The Cinemachine camera target. If not set, will use ThirdPersonController's CinemachineCameraTarget.")]
    public GameObject CinemachineCameraTarget;

    [Header("Camera Rotation Settings")]
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;

    [Tooltip("Additional degrees to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    [Header("Enhanced Camera Features")]
    [Tooltip("Maximum rotation speed for camera (degrees per second). Set to 0 for unlimited.")]
    public float MaxRotationSpeed = 180.0f;

    [Tooltip("Lock camera pitch (prevent up/down movement)")]
    public bool LockCameraPitch = true;

    [Tooltip("Use keyboard (Q/E) instead of mouse for camera rotation")]
    public bool UseKeyboardCamera = false;

    [Tooltip("Keyboard camera rotation speed (degrees per second)")]
    public float KeyboardRotationSpeed = 90.0f;

    // Internal camera state
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    // References
    private ThirdPersonController _thirdPersonController;
    private StarterAssetsInputs _input;
#if ENABLE_INPUT_SYSTEM 
    private PlayerInput _playerInput;
#endif

    private const float _threshold = 0.01f;

    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
#else
            return false;
#endif
        }
    }

    private void Awake()
    {
        _thirdPersonController = GetComponent<ThirdPersonController>();
        
        if (_thirdPersonController == null)
        {
            Debug.LogError("[DaydreamCameraController] ThirdPersonController not found on this GameObject. This component requires ThirdPersonController.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Get references
        _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM 
        _playerInput = GetComponent<PlayerInput>();
#endif

        // Use ThirdPersonController's camera target if not explicitly set
        if (CinemachineCameraTarget == null && _thirdPersonController != null)
        {
            CinemachineCameraTarget = _thirdPersonController.CinemachineCameraTarget;
        }

        if (CinemachineCameraTarget == null)
        {
            Debug.LogError("[DaydreamCameraController] CinemachineCameraTarget not found. Please assign it in the inspector.");
            enabled = false;
            return;
        }

        // Initialize camera rotation from current target rotation
        _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        _cinemachineTargetPitch = CinemachineCameraTarget.transform.rotation.eulerAngles.x;
        
        // Normalize pitch to handle values > 180
        if (_cinemachineTargetPitch > 180f)
        {
            _cinemachineTargetPitch -= 360f;
        }

        // Disable ThirdPersonController's camera rotation - we'll handle it
        if (_thirdPersonController != null)
        {
            _thirdPersonController.LockCameraPosition = true;
        }
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    private void CameraRotation()
    {
        if (CinemachineCameraTarget == null || _input == null)
            return;

        // Check for R key to reset camera rotation
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetCameraRotation();
        }
#endif

        if (!LockCameraPosition)
        {
            if (UseKeyboardCamera)
            {
                // Keyboard camera control with Q/E keys
#if ENABLE_INPUT_SYSTEM
                float keyboardYawInput = 0f;

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.qKey.isPressed)
                    {
                        keyboardYawInput = -KeyboardRotationSpeed * Time.deltaTime;
                        Debug.Log($"[DaydreamCamera] Q pressed, rotating left: {keyboardYawInput}");
                    }
                    else if (Keyboard.current.eKey.isPressed)
                    {
                        keyboardYawInput = KeyboardRotationSpeed * Time.deltaTime;
                        Debug.Log($"[DaydreamCamera] E pressed, rotating right: {keyboardYawInput}");
                    }
                }

                _cinemachineTargetYaw += keyboardYawInput;
#endif
            }
            else
            {
                // Mouse camera control (original behavior with enhancements)
                if (_input.look.sqrMagnitude >= _threshold)
                {
                    // Don't multiply mouse input by Time.deltaTime
                    float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                    float yawInput = _input.look.x * deltaTimeMultiplier;
                    float pitchInput = _input.look.y * deltaTimeMultiplier;

                    // Apply rotation speed cap if set
                    if (MaxRotationSpeed > 0)
                    {
                        float maxDelta = MaxRotationSpeed * Time.deltaTime;
                        yawInput = Mathf.Clamp(yawInput, -maxDelta, maxDelta);
                        pitchInput = Mathf.Clamp(pitchInput, -maxDelta, maxDelta);
                    }

                    _cinemachineTargetYaw += yawInput;

                    // Only update pitch if not locked
                    if (!LockCameraPitch)
                    {
                        _cinemachineTargetPitch += pitchInput;
                    }
                }
            }
        }

        // Clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
            _cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw, 
            0.0f
        );
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    /// <summary>
    /// Set the camera yaw rotation programmatically.
    /// </summary>
    public void SetYaw(float yaw)
    {
        _cinemachineTargetYaw = yaw;
    }

    /// <summary>
    /// Set the camera pitch rotation programmatically.
    /// </summary>
    public void SetPitch(float pitch)
    {
        _cinemachineTargetPitch = Mathf.Clamp(pitch, BottomClamp, TopClamp);
    }

    /// <summary>
    /// Get the current camera yaw rotation.
    /// </summary>
    public float GetYaw()
    {
        return _cinemachineTargetYaw;
    }

    /// <summary>
    /// Get the current camera pitch rotation.
    /// </summary>
    public float GetPitch()
    {
        return _cinemachineTargetPitch;
    }

    /// <summary>
    /// Reset camera rotation to behind the player (yaw = 0, pitch = 0).
    /// </summary>
    public void ResetCameraRotation()
    {
        _cinemachineTargetYaw = 0f;
        _cinemachineTargetPitch = 0f;
        Debug.Log("[DaydreamCameraController] Camera rotation reset to start position");
    }
}
