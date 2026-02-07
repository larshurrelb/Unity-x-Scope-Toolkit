#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DaydreamAPIManager))]
public class DaydreamAPIManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty resolutionPresetProp = serializedObject.FindProperty("resolutionPreset");
        SerializedProperty widthProp = serializedObject.FindProperty("width");
        SerializedProperty heightProp = serializedObject.FindProperty("height");
        SerializedProperty pipelinePresetProp = serializedObject.FindProperty("pipelinePreset");

        ResolutionPreset currentPreset = (ResolutionPreset)resolutionPresetProp.enumValueIndex;
        PipelinePreset currentPipeline = (PipelinePreset)pipelinePresetProp.enumValueIndex;

        // Draw all properties with conditional logic
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script field
            if (iterator.propertyPath == "m_Script")
                continue;

            // For width/height: show based on resolution preset
            if (iterator.propertyPath == "width" || iterator.propertyPath == "height")
            {
                if (currentPreset == ResolutionPreset.Custom)
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                else
                {
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(iterator, true);
                    GUI.enabled = true;
                }
            }
            // For kvCacheAttentionBias: only show when Krea pipeline selected
            else if (iterator.propertyPath == "kvCacheAttentionBias")
            {
                if (currentPipeline == PipelinePreset.KreaRealtimeVideo)
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        // Show resolution info
        if (currentPreset != ResolutionPreset.Custom)
        {
            EditorGUILayout.HelpBox(
                $"Resolution: {widthProp.intValue} x {heightProp.intValue} (16:9). Select 'Custom' to edit manually.",
                MessageType.Info);
        }

        // Show pipeline info box
        EditorGUILayout.Space(5);
        switch (currentPipeline)
        {
            case PipelinePreset.LongLive:
                EditorGUILayout.HelpBox(
                    "LongLive (longlive)\n" +
                    "General-purpose pipeline. ~20GB VRAM.\n" +
                    "Default V2V: 512x512, steps [1000, 750]",
                    MessageType.Info);
                break;
            case PipelinePreset.StreamDiffusionV2:
                EditorGUILayout.HelpBox(
                    "StreamDiffusion V2 (streamdiffusionv2)\n" +
                    "Optimized for real-time V2V. ~20GB VRAM.\n" +
                    "Default V2V: 320x576, steps [750, 250]",
                    MessageType.Info);
                break;
            case PipelinePreset.MemFlow:
                EditorGUILayout.HelpBox(
                    "MemFlow (memflow)\n" +
                    "Memory-based temporal consistency. ~20GB VRAM.\n" +
                    "Default V2V: 512x512, steps [1000, 750]",
                    MessageType.Info);
                break;
            case PipelinePreset.KreaRealtimeVideo:
                EditorGUILayout.HelpBox(
                    "Krea Realtime Video (krea-realtime-video)\n" +
                    "14B parameter model. Requires 32GB+ VRAM.\n" +
                    "Default V2V: 256x256, steps [1000, 750]\n" +
                    "Supports kv_cache_attention_bias parameter.",
                    MessageType.Warning);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
