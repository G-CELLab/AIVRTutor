using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ApiKeyBuildInjector : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string ResourcesDir = "Assets/Resources";
    private const string AssetPath    = ResourcesDir + "/ApiKeyConfig.asset";

    // Fallback file for machines where setting a real OS env var is inconvenient.
    // Put ONE line in this file: your key. Keep it out of git (see .gitignore step).
    private const string LocalSecretsFile = "local.secrets/openai.key";

    public void OnPreprocessBuild(BuildReport report)
    {
        string key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(key) && File.Exists(LocalSecretsFile))
            key = File.ReadAllText(LocalSecretsFile).Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[ApiKeyBuildInjector] No OPENAI_API_KEY found (env var or " +
                              LocalSecretsFile + "). Building without a baked-in key.");
            return;
        }

        if (!Directory.Exists(ResourcesDir))
            Directory.CreateDirectory(ResourcesDir);

        var config = AssetDatabase.LoadAssetAtPath<ApiKeyConfig>(AssetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ApiKeyConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
        }

        config.SetKey(key);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[ApiKeyBuildInjector] API key injected into ApiKeyConfig for this build.");
    }
}