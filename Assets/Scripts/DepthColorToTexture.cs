using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthColorToTexture : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RenderTexture depthTexture;
    [SerializeField] private Material depthMaterial;
    [SerializeField] private LayerMask excludeLayers; // Layers to exclude from depth
    [SerializeField] private LayerMask characterLayers; // Layers for characters to render in third color
    [SerializeField] private Shader characterMaskShader; // Shader for character mask rendering (assign in inspector as fallback)

    private int originalCullingMask;
    private RenderTexture characterMaskTexture;
    private Camera characterCamera;
    private GameObject characterCameraObject;
    
    // Public getter for the depth material
    public Material DepthMaterial => depthMaterial;

    void Start()
    {
        var cameraData = targetCamera.GetUniversalAdditionalCameraData();
        cameraData.requiresDepthTexture = true;
        
        // Set camera aspect ratio based on render texture dimensions
        if (depthTexture != null)
        {
            targetCamera.aspect = (float)depthTexture.width / (float)depthTexture.height;
        }
        
        if (depthTexture != null)
        {
            depthTexture.filterMode = FilterMode.Point;
            depthTexture.anisoLevel = 0;
            
            // Create character mask texture with same dimensions
            characterMaskTexture = new RenderTexture(depthTexture.width, depthTexture.height, 0, RenderTextureFormat.R8);
            characterMaskTexture.filterMode = FilterMode.Point;
            characterMaskTexture.anisoLevel = 0;
        }
        
        // Store original culling mask
        originalCullingMask = targetCamera.cullingMask;
        
        // Create a separate camera for character mask rendering
        SetupCharacterCamera();
    }
    
    void SetupCharacterCamera()
    {
        // Create a child camera object
        characterCameraObject = new GameObject("CharacterMaskCamera");
        characterCameraObject.transform.SetParent(targetCamera.transform);
        characterCameraObject.transform.localPosition = Vector3.zero;
        characterCameraObject.transform.localRotation = Quaternion.identity;
        
        // Add camera component
        characterCamera = characterCameraObject.AddComponent<Camera>();
        characterCamera.CopyFrom(targetCamera);
        
        // Configure for character mask rendering
        characterCamera.cullingMask = characterLayers.value;
        characterCamera.backgroundColor = Color.black;
        characterCamera.clearFlags = CameraClearFlags.SolidColor;
        characterCamera.targetTexture = characterMaskTexture;
        characterCamera.depth = targetCamera.depth - 1; // Render before main camera
        characterCamera.enabled = true;
        
        // Use replacement shader to render everything as white
        // Try serialized reference first, then fall back to Shader.Find
        Shader whiteShader = characterMaskShader != null ? characterMaskShader : Shader.Find("Hidden/CharacterMask");
        if (whiteShader != null)
        {
            characterCamera.SetReplacementShader(whiteShader, "");
        }
        else
        {
            Debug.LogError("[DepthColorToTexture] CharacterMask shader not found! Assign it in the inspector or add to Always Included Shaders.");
        }
        
        // Disable URP features we don't need
        var charCamData = characterCamera.GetUniversalAdditionalCameraData();
        if (charCamData != null)
        {
            charCamData.renderPostProcessing = false;
            charCamData.renderShadows = false;
            charCamData.requiresDepthTexture = false;
            charCamData.requiresColorTexture = false;
        }
    }

    void OnPreRender()
    {
        // Temporarily exclude layers before rendering
        targetCamera.cullingMask = originalCullingMask & ~excludeLayers.value;
    }

    void OnPostRender()
    {
        // Restore original culling mask after rendering
        targetCamera.cullingMask = originalCullingMask;
    }

    void LateUpdate()
    {
        // Sync character camera with target camera
        if (characterCamera != null && targetCamera != null)
        {
            characterCamera.fieldOfView = targetCamera.fieldOfView;
            characterCamera.nearClipPlane = targetCamera.nearClipPlane;
            characterCamera.farClipPlane = targetCamera.farClipPlane;
            characterCamera.aspect = targetCamera.aspect;
        }
    }

    void OnRenderObject()
    {
        if (targetCamera == null || depthTexture == null || characterMaskTexture == null) return;
        
        // Pass character mask to material
        if (depthMaterial != null)
        {
            depthMaterial.SetTexture("_CharacterMask", characterMaskTexture);
        }
        
        Texture depthSource = Shader.GetGlobalTexture("_CameraDepthTexture");
        Graphics.Blit(depthSource, depthTexture, depthMaterial);
    }
    
    void OnDestroy()
    {
        if (characterMaskTexture != null)
        {
            characterMaskTexture.Release();
            Destroy(characterMaskTexture);
        }
        
        if (characterCameraObject != null)
        {
            Destroy(characterCameraObject);
        }
    }
}
