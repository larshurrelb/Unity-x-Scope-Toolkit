#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DaydreamAPIManager))]
public class DaydreamAPIManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw all properties except width/height
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        SerializedProperty resolutionPresetProp = serializedObject.FindProperty("resolutionPreset");
        SerializedProperty widthProp = serializedObject.FindProperty("width");
        SerializedProperty heightProp = serializedObject.FindProperty("height");

        ResolutionPreset currentPreset = (ResolutionPreset)resolutionPresetProp.enumValueIndex;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script field
            if (iterator.propertyPath == "m_Script")
                continue;

            // For width/height, show based on preset
            if (iterator.propertyPath == "width" || iterator.propertyPath == "height")
            {
                if (currentPreset == ResolutionPreset.Custom)
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                else
                {
                    // Show as read-only label
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(iterator, true);
                    GUI.enabled = true;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        // Show helpful info
        if (currentPreset != ResolutionPreset.Custom)
        {
            EditorGUILayout.HelpBox($"Resolution: {widthProp.intValue} x {heightProp.intValue} (16:9). Select 'Custom' to edit manually.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
