using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public struct DataStructure
{
    public string Name;
    public string Path;
    public string TypeName;
}

public class DataManager : GlobalSingleton<DataManager>
{
    [SerializeField]
    private List<DataStructure> _inputData = new List<DataStructure>();
    private Dictionary<Type, object> _outputData = new Dictionary<Type, object>();
    private IDataLoader _loader = new JsonResourcesDataLoader();

    public bool DataLoaded { get; private set; }

    public event Action OnDataLoaded;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        DataLoaded = false;

        LoadAll();
    }

    private void LoadAll()
    {
        if (_inputData != null)
        {
            foreach (var data in _inputData)
            {
                Type type = Type.GetType(data.TypeName);
                if (type == null)
                {
                    Debug.LogError($"[DataManager] '{data.Name}': type '{data.TypeName}' not found. Namespaced classes need the full name.");
                    continue;
                }

                if (_loader.TryLoad(data.Path, type, out var outputData))
                {
                    _outputData[type] = outputData;
                }
                else
                {
                    Debug.LogError($"[DataManager] '{data.Name}': failed to load {type.Name} from Resources/{data.Path}");
                }
            }

            DataLoaded = true;
            OnDataLoaded?.Invoke();

            Debug.Log("[DataManager] Data successfully loaded!");
        }
        else
        {
            Debug.LogWarning("[DataManager] Data failed to load");
        }
    }

    public T Get<T>() where T : class
    {
        if (TryGet<T>(out var data))
        {
            return data;
        }

        Debug.LogWarning($"[DataManager] No data loaded for {typeof(T).Name}");
        return null;
    }

    public bool TryGet<T>(out T data) where T : class
    {
        if (_outputData.TryGetValue(typeof(T), out var obj))
        {
            data = obj as T;
            return data != null;
        }

        data = null;
        return false;
    }
}
