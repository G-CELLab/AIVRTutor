using UnityEngine;

[CreateAssetMenu(fileName = "ApiKeyConfig", menuName = "Config/API Key Config")]
public class ApiKeyConfig : ScriptableObject
{
    [SerializeField] private string openAIKey = "";
    public string OpenAIKey => openAIKey;

    // Only ever called by the editor build script, never at runtime.
    public void SetKey(string key) => openAIKey = key;
}