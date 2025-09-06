using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GeneratePyramid))]
public class GeneratePyramidEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (for all public variables)
        DrawDefaultInspector();

        // Get a reference to the script being inspected
        GeneratePyramid pyramidGenerator = (GeneratePyramid)target;

        // Add some space for better layout
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        // Create a horizontal layout for the buttons
        EditorGUILayout.BeginHorizontal();

        // "Apply" button
        if (GUILayout.Button("Apply"))
        {
            // Call the method to create the pyramid
            pyramidGenerator.CreatePyramid();
        }

        // "Reset" button
        if (GUILayout.Button("Reset"))
        {
            // Call the method to reset values to their defaults
            pyramidGenerator.ResetValues();
        }
        
        EditorGUILayout.EndHorizontal();
        
        // "Delete" button on its own row
        if (GUILayout.Button("Delete Pyramid"))
        {
            // Add a confirmation dialog to prevent accidental deletion
            if (EditorUtility.DisplayDialog("Delete Pyramid",
                "Are you sure you want to delete the pyramid? This action cannot be undone.", "Yes, Delete", "No"))
            {
                pyramidGenerator.ClearPyramid(true);
            }
        }
    }
}
