using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders OpenPose-style skeleton visualization on player armature.
/// Draws colored joints and bones that appear in the camera's RenderTexture output.
/// Attaches to the same camera that has DepthColorToTexture component.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OpenPoseSkeletonRenderer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's Animator component. Will auto-find if not assigned.")]
    public Animator playerAnimator;
    
    [Tooltip("Source video texture (from camera/depth rendering)")]
    public RenderTexture sourceVideoTexture;
    
    [Tooltip("Combined output texture with skeleton overlay applied")]
    public RenderTexture combinedVideoTexture;
    
    [Header("Rendering Settings")]
    [Tooltip("Enable/disable OpenPose skeleton visualization")]
    public bool showOpenPose = false;
    
    [Tooltip("Radius of joint spheres in world units")]
    [Range(0.01f, 0.5f)]
    public float jointSphereSize = 0.05f;
    
    [Tooltip("Width of bone lines in pixels")]
    [Range(1f, 10f)]
    public float lineWidth = 3f;
    
    [Tooltip("Render skeleton on top of everything (ignore depth)")]
    public bool alwaysOnTop = true;
    
    private Camera cam;
    private Material lineMaterial;
    
    // OpenPose joint data
    private struct Joint
    {
        public int id;
        public string name;
        public Color color;
        public HumanBodyBones unityBone;
        public Transform cachedTransform;
    }
    
    // OpenPose bone connection data
    private struct Bone
    {
        public int joint1Index;
        public int joint2Index;
        public string name;
        public Color color;
    }
    
    private Joint[] joints;
    private Bone[] bones;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        InitializeOpenPoseData();
        CacheBoneTransforms();
        CreateLineMaterial();
    }
    
    void InitializeOpenPoseData()
    {
        // Define 18 OpenPose joints with colors and Unity bone mapping
        joints = new Joint[]
        {
            new Joint { id = 0, name = "Nose", color = new Color(1f, 0f, 0f), unityBone = HumanBodyBones.Head }, // Approximate
            new Joint { id = 1, name = "Neck", color = new Color(1f, 0.33f, 0f), unityBone = HumanBodyBones.Neck },
            new Joint { id = 2, name = "Right Shoulder", color = new Color(1f, 0.67f, 0f), unityBone = HumanBodyBones.RightShoulder },
            new Joint { id = 3, name = "Right Elbow", color = new Color(1f, 1f, 0f), unityBone = HumanBodyBones.RightLowerArm },
            new Joint { id = 4, name = "Right Wrist", color = new Color(0.67f, 1f, 0f), unityBone = HumanBodyBones.RightHand },
            new Joint { id = 5, name = "Left Shoulder", color = new Color(0.33f, 1f, 0f), unityBone = HumanBodyBones.LeftShoulder },
            new Joint { id = 6, name = "Left Elbow", color = new Color(0f, 1f, 0f), unityBone = HumanBodyBones.LeftLowerArm },
            new Joint { id = 7, name = "Left Wrist", color = new Color(0f, 1f, 0.33f), unityBone = HumanBodyBones.LeftHand },
            new Joint { id = 8, name = "Right Hip", color = new Color(0f, 1f, 0.67f), unityBone = HumanBodyBones.RightUpperLeg },
            new Joint { id = 9, name = "Right Knee", color = new Color(0f, 1f, 1f), unityBone = HumanBodyBones.RightLowerLeg },
            new Joint { id = 10, name = "Right Ankle", color = new Color(0f, 0.67f, 1f), unityBone = HumanBodyBones.RightFoot },
            new Joint { id = 11, name = "Left Hip", color = new Color(0f, 0.33f, 1f), unityBone = HumanBodyBones.LeftUpperLeg },
            new Joint { id = 12, name = "Left Knee", color = new Color(0f, 0f, 1f), unityBone = HumanBodyBones.LeftLowerLeg },
            new Joint { id = 13, name = "Left Ankle", color = new Color(0.33f, 0f, 1f), unityBone = HumanBodyBones.LeftFoot },
            new Joint { id = 14, name = "Right Eye", color = new Color(0.67f, 0f, 1f), unityBone = HumanBodyBones.RightEye },
            new Joint { id = 15, name = "Left Eye", color = new Color(1f, 0f, 1f), unityBone = HumanBodyBones.LeftEye },
            new Joint { id = 16, name = "Right Ear", color = new Color(1f, 0f, 0.67f), unityBone = HumanBodyBones.Head }, // Approximate
            new Joint { id = 17, name = "Left Ear", color = new Color(1f, 0f, 0.33f), unityBone = HumanBodyBones.Head }  // Approximate
        };
        
        // Define bone connections (pairs of joint indices) with darker colors
        bones = new Bone[]
        {
            new Bone { joint1Index = 1, joint2Index = 2, name = "Right Shoulderblade", color = new Color(0.6f, 0f, 0f) },
            new Bone { joint1Index = 1, joint2Index = 5, name = "Left Shoulderblade", color = new Color(0.6f, 0.2f, 0f) },
            new Bone { joint1Index = 2, joint2Index = 3, name = "Right Arm", color = new Color(0.6f, 0.4f, 0f) },
            new Bone { joint1Index = 3, joint2Index = 4, name = "Right Forearm", color = new Color(0.6f, 0.6f, 0f) },
            new Bone { joint1Index = 5, joint2Index = 6, name = "Left Arm", color = new Color(0.4f, 0.6f, 0f) },
            new Bone { joint1Index = 6, joint2Index = 7, name = "Left Forearm", color = new Color(0.2f, 0.6f, 0f) },
            new Bone { joint1Index = 1, joint2Index = 8, name = "Right Torso", color = new Color(0f, 0.6f, 0f) },
            new Bone { joint1Index = 8, joint2Index = 9, name = "Right Upper Leg", color = new Color(0f, 0.6f, 0.2f) },
            new Bone { joint1Index = 9, joint2Index = 10, name = "Right Lower Leg", color = new Color(0f, 0.6f, 0.4f) },
            new Bone { joint1Index = 1, joint2Index = 11, name = "Left Torso", color = new Color(0f, 0.6f, 0.6f) },
            new Bone { joint1Index = 11, joint2Index = 12, name = "Left Upper Leg", color = new Color(0f, 0.4f, 0.6f) },
            new Bone { joint1Index = 12, joint2Index = 13, name = "Left Lower Leg", color = new Color(0f, 0.2f, 0.6f) },
            new Bone { joint1Index = 1, joint2Index = 0, name = "Head", color = new Color(0f, 0f, 0.6f) },
            new Bone { joint1Index = 0, joint2Index = 14, name = "Right Eyebrow", color = new Color(0.2f, 0f, 0.6f) },
            new Bone { joint1Index = 14, joint2Index = 16, name = "Right Ear", color = new Color(0.4f, 0f, 0.6f) },
            new Bone { joint1Index = 0, joint2Index = 15, name = "Left Eyebrow", color = new Color(0.6f, 0f, 0.6f) },
            new Bone { joint1Index = 15, joint2Index = 17, name = "Left Ear", color = new Color(0.6f, 0f, 0.4f) }
        };
    }
    
    void CacheBoneTransforms()
    {
        // Auto-find player animator if not assigned
        if (playerAnimator == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerAnimator = player.GetComponentInChildren<Animator>();
            }
            
            if (playerAnimator == null)
            {
                Debug.LogWarning("[OpenPoseRenderer] Could not find player Animator. Assign manually or tag player as 'Player'");
                return;
            }
        }
        
        // Cache transform references for each joint
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].unityBone != HumanBodyBones.LastBone)
            {
                joints[i].cachedTransform = playerAnimator.GetBoneTransform(joints[i].unityBone);
                
                if (joints[i].cachedTransform == null)
                {
                    Debug.LogWarning($"[OpenPoseRenderer] Could not find bone transform for {joints[i].name} ({joints[i].unityBone})");
                }
            }
        }
        
        Debug.Log($"[OpenPoseRenderer] Cached {joints.Length} joint transforms from player animator");
    }
    
    void CreateLineMaterial()
    {
        // Create material for GL line rendering
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", alwaysOnTop ? (int)UnityEngine.Rendering.CompareFunction.Always : (int)UnityEngine.Rendering.CompareFunction.LessEqual);
    }
    
    void LateUpdate()
    {
        if (!showOpenPose || playerAnimator == null || lineMaterial == null)
            return;
        
        if (sourceVideoTexture == null || combinedVideoTexture == null)
        {
            Debug.LogWarning("[OpenPoseRenderer] Source or combined video texture not assigned!");
            return;
        }
        
        // Copy source texture to combined texture
        Graphics.Blit(sourceVideoTexture, combinedVideoTexture);
        
        // Render skeleton overlay on top
        RenderTexture.active = combinedVideoTexture;
        
        // Set GL state
        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.modelview = cam.worldToCameraMatrix;
        
        // Draw bones (lines between joints)
        GL.Begin(GL.LINES);
        foreach (var bone in bones)
        {
            Transform t1 = joints[bone.joint1Index].cachedTransform;
            Transform t2 = joints[bone.joint2Index].cachedTransform;
            
            if (t1 != null && t2 != null)
            {
                GL.Color(bone.color);
                GL.Vertex(t1.position);
                GL.Vertex(t2.position);
            }
        }
        GL.End();
        
        // Draw joints (as small crosses for visibility)
        GL.Begin(GL.LINES);
        foreach (var joint in joints)
        {
            if (joint.cachedTransform != null)
            {
                Vector3 pos = joint.cachedTransform.position;
                Vector3 right = cam.transform.right * jointSphereSize;
                Vector3 up = cam.transform.up * jointSphereSize;
                Vector3 forward = cam.transform.forward * jointSphereSize;
                
                GL.Color(joint.color);
                
                // Draw 3 axes cross at joint position
                GL.Vertex(pos - right);
                GL.Vertex(pos + right);
                GL.Vertex(pos - up);
                GL.Vertex(pos + up);
                GL.Vertex(pos - forward);
                GL.Vertex(pos + forward);
            }
        }
        GL.End();
        
        GL.PopMatrix();
        RenderTexture.active = null;
    }
    
    void OnDestroy()
    {
        if (lineMaterial != null)
        {
            DestroyImmediate(lineMaterial);
        }
    }
    
    // Re-cache bone transforms if animator changes
    void OnValidate()
    {
        if (Application.isPlaying && playerAnimator != null)
        {
            CacheBoneTransforms();
        }
        
        if (lineMaterial != null)
        {
            lineMaterial.SetInt("_ZTest", alwaysOnTop ? (int)UnityEngine.Rendering.CompareFunction.Always : (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }
    }
}
