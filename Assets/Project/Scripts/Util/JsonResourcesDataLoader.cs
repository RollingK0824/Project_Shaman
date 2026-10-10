using System;
using UnityEngine;

public class JsonResourcesDataLoader : IDataLoader
{
    public bool TryLoad<T>(string key, out T data)
    {
        data = default;

        if (!TryLoadText(key, out string json))
        {
            return false;
        }

        return DataParser.TryParse<T>(json, out data);
    }

    public bool TryLoad(string key, Type type, out object data)
    {
        data = null;

        if (!TryLoadText(key, out string json))
        {
            return false;
        }

        return DataParser.TryParse(json, type, out data);
    }

    private bool TryLoadText(string key, out string json)
    {
        json = null;

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

        json = textAsset.text;
        return true;
    }
}
