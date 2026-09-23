using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
public class ObjectPoolManager : GlobalSingleton<ObjectPoolManager>
{
    [Header("Pool Sizing Defaults")]
    [SerializeField] private int defaultCapacity = 10;
    [SerializeField] private int maxSize = 50;

    private readonly Dictionary<GameObject, IObjectPool<GameObject>> _pools = new Dictionary<GameObject, IObjectPool<GameObject>>();

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPoolManager] Cannot pool null prefab!");
            return null;
        }
        IObjectPool<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public void Release(GameObject prefabKey, GameObject instance)
    {
        if (instance == null || prefabKey == null) return;
        if (_pools.TryGetValue(prefabKey, out IObjectPool<GameObject> pool))
        {
            pool.Release(instance);
        }
        else
        {
            Destroy(instance);
        }
    }

    private IObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out IObjectPool<GameObject> existingPool))
        {
            return existingPool;
        }
        IObjectPool<GameObject> newPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(prefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: true, 
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
        _pools.Add(prefab, newPool);
        return newPool;
    }
}