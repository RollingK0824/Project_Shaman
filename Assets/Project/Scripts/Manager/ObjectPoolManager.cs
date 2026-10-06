using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
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
        // 보관 중인 오브젝트만 영속 풀 아래에 두고, 사용 중에는 현재 씬에 배치합니다.
        instance.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
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
            createFunc: () =>
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                return obj;
            },
            actionOnGet: null,
            actionOnRelease: (obj) =>
            {
                obj.SetActive(false);
                obj.transform.SetParent(transform);
            },
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: true, 
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
        _pools.Add(prefab, newPool);
        return newPool;
    }
}
