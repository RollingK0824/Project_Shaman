using UnityEngine;

public class JsonResourcesDataLoader : IDataLoader
{
    public bool TryLoad<T>(string key, out T data)
    {
        data = default;
        
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[JsonResourcesDataLoader] Load key cannot be null or empty.");
            return false;
        }
        
        TextAsset textAsset = Resources.Load<TextAsset>(key);
        if (textAsset == null)
        {
            Debug.LogWarning($"[JsonResourcesDataLoader] Could not find JSON TextAsset at path: Resources/'{key}'");
            return false;
        }
        
        return DataParser.TryParse<T>(textAsset.text, out data);
    }
}
