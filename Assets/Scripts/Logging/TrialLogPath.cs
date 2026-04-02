using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Provides a single timestamped output folder for one app run.
/// All research artifacts should be written through this helper so files can be grouped by participant session.
/// </summary>
public static class TrialLogPath
{
    private const string RootFolderName = "trial_logs";
    private static string runDirectoryPath;

    public static string GetRunDirectoryPath()
    {
        if (!string.IsNullOrEmpty(runDirectoryPath))
            return runDirectoryPath;

        string root = Path.Combine(Application.persistentDataPath, RootFolderName);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        runDirectoryPath = Path.Combine(root, timestamp);

        Directory.CreateDirectory(runDirectoryPath);
        Debug.Log("[TrialLogPath] Session log directory: " + runDirectoryPath);
        return runDirectoryPath;
    }

    public static string GetFilePath(string fileName)
    {
        return Path.Combine(GetRunDirectoryPath(), fileName);
    }
}