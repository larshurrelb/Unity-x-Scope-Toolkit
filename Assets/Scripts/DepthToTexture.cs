using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthToTexture : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RenderTexture depthTexture;
    [SerializeField] private Material depthMaterial;

    void Start()
    {
        if (targetCamera == null)
        {
            Debug.LogError("[DepthToTexture] Target camera is not assigned!");
            return;
        }

        var cameraData = targetCamera.GetUniversalAdditionalCameraData();
        cameraData.requiresDepthTexture = true;
        
        // Set camera aspect ratio based on render texture dimensions
        if (depthTexture != null)
        {
            targetCamera.aspect = (float)depthTexture.width / (float)depthTexture.height;
            depthTexture.filterMode = FilterMode.Point;
            depthTexture.anisoLevel = 0;
        }
    }

    void OnRenderObject()
    {
        if (targetCamera == null || depthTexture == null || depthMaterial == null) return;
        
        Texture depthSource = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthSource != null)
        {
            Graphics.Blit(depthSource, depthTexture, depthMaterial);
        }
    }
}
