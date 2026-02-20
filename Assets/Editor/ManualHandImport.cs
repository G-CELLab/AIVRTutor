/*
2026-02-20 AI-Tag
This was created with the help of Assistant, a Unity Artificial Intelligence product.
*/
using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Reflection;

public class ManualHandImport : EditorWindow
{
    [MenuItem("Tools/Convert Local .handsbin to Asset")]
    public static void ConvertRecording()
    {
        // 1. Let the user select the .handsbin file from LocalLow
        string localLowPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "TaehyunKim", "Biology_ChromosoME_withG");
        string filePath = EditorUtility.OpenFilePanel("Select .handsbin recording", localLowPath, "handsbin");

        if (string.IsNullOrEmpty(filePath)) return;

        // 2. Use Reflection to call the internal Unity conversion method
        var handsAssembly = Assembly.Load("Unity.XR.Hands");
        var blobType = handsAssembly.GetType("UnityEngine.XR.Hands.Capture.Recording.XRHandRecordingBlob");
        var sequenceType = handsAssembly.GetType("UnityEngine.XR.Hands.Capture.XRHandCaptureSequence");
        
        var method = blobType.GetMethod("TryReadCaptureSequenceFromDisk", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        
        if (method != null)
        {
            object[] parameters = new object[] { filePath, null };
            bool success = (bool)method.Invoke(null, parameters);
            
            if (success)
            {
                ScriptableObject recordingAsset = (ScriptableObject)parameters[1];
                
                // 3. Save as a Unity Asset
                string savePath = "Assets/MyHandRecording.asset";
                AssetDatabase.CreateAsset(recordingAsset, savePath);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"Successfully created asset at: {savePath}");
                Selection.activeObject = recordingAsset;
            }
            else
            {
                Debug.LogError("Failed to convert .handsbin data.");
            }
        }
    }
}
