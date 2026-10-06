using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using UnityEngine;

public class DataParser
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() }
    };

    public static T Parse<T> (string json) 
    {
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }

    public static bool TryParse<T>(string json, out T result)
    {
        result = default;
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[DataParser] JSON input string is null or empty.");
            return false;
        }

        try
        {
            result = Parse<T>(json);
            return result != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataParser] Failed to parse JSON into {typeof(T).Name}: {ex.Message}");
            result = default;
            return false;
        }
    }
}
