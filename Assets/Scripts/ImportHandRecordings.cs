/*
2026-02-20 AI-Tag
This was created with the help of Assistant, a Unity Artificial Intelligence product.
*/
using System;
using UnityEditor;
using UnityEngine;
using System.IO;

public class ImportHandRecordings : EditorWindow
{
    [MenuItem("Tools/Import Local Hand Recordings")]
    public static void Import()
    {
        string sourceDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "TaehyunKim", "Biology_ChromosoME_withG");
        string projectTarget = Path.Combine(Directory.GetCurrentDirectory(), "RecordedHandData");

        if (!Directory.Exists(sourceDir))
        {
            Debug.LogError("Source directory not found: " + sourceDir);
            return;
        }

        if (!Directory.Exists(projectTarget))
            Directory.CreateDirectory(projectTarget);

        string[] files = Directory.GetFiles(sourceDir, "*.handsbin");
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(projectTarget, fileName);
            File.Copy(file, destFile, true);
            Debug.Log($"Copied {fileName} to project root /RecordedHandData/");
        }

        Debug.Log("Import Complete. Now try clicking 'Import Recordings' in the XR Hand Capture window.");
    }
}
