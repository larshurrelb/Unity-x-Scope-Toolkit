using UnityEngine;
using UnityEditor;

/// <summary>
/// Helper utility to create RenderTextures with the correct format for Daydream streaming.
/// </summary>
public class DaydreamTextureHelper : EditorWindow
{
    private int width = 512;
    private int height = 512;
    private string textureName = "DaydreamInputTexture";
    
    [MenuItem("Daydream/Create WebRTC-Compatible RenderTexture")]
    public static void ShowWindow()
    {
        GetWindow<DaydreamTextureHelper>("Create WebRTC Texture");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Create WebRTC-Compatible RenderTexture", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "WebRTC streaming requires RenderTextures with B8G8R8A8_SRGB format. " +
            "This tool creates a properly configured RenderTexture for use with DaydreamAPIManager.",
            MessageType.Info);
        
        GUILayout.Space(10);
        
        textureName = EditorGUILayout.TextField("Texture Name", textureName);
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Create RenderTexture", GUILayout.Height(30)))
        {
            CreateWebRTCRenderTexture();
        }
        
        GUILayout.Space(20);
        
        EditorGUILayout.HelpBox(
            "After creating the texture:\n" +
            "1. Assign it to your DaydreamAPIManager's Input Video Texture field\n" +
            "2. Set up a camera or other source to render into this texture\n" +
            "3. Make sure the camera's Target Texture is set to this RenderTexture",
            MessageType.Info);
    }
    
    private void CreateWebRTCRenderTexture()
    {
        // Create the RenderTexture with the correct format
        RenderTexture rt = new RenderTexture(width, height, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_SRGB);
        rt.name = textureName;
        rt.autoGenerateMips = false;
        rt.useMipMap = false;
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        
        // Save it as an asset
        string path = $"Assets/{textureName}.renderTexture";
        AssetDatabase.CreateAsset(rt, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select and ping the asset
        EditorGUIUtility.PingObject(rt);
        Selection.activeObject = rt;
        
        Debug.Log($"✅ Created WebRTC-compatible RenderTexture: {path} ({width}x{height}, B8G8R8A8_SRGB)");
        
        EditorUtility.DisplayDialog(
            "RenderTexture Created",
            $"Successfully created '{textureName}' at:\n{path}\n\n" +
            "Format: B8G8R8A8_SRGB (WebRTC compatible)\n" +
            $"Size: {width}x{height}\n\n" +
            "The texture has been selected in the Project window.",
            "OK");
    }
}
