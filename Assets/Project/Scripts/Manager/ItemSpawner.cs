using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ItemSpawner : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private ItemSpawnerConfig config;
    [Header("Spawn Area Settings")]
    [SerializeField] private Collider areaCollider;

    // 네트워크 어댑터가 구독합니다. 아이템과 위치는 요청을 받은 서버에서 결정합니다.
    public event Action SpawnRequested;

    public ItemSpawnerConfig Config => config;
    public bool HasSpawnableItems => config != null && config.HasSpawnableItems;

    private void Awake()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<Collider>();
        }
    }

    public void RequestSpawn()
    {
        if (!HasSpawnableItems)
        {
            Debug.LogWarning($"[ItemSpawner] Config가 없거나 스폰 가능한 아이템이 없습니다: {gameObject.name}", this);
            return;
        }
        SpawnRequested?.Invoke();
    }

    public GameObject GetRandomItemPrefab()
    {
        return config != null ? config.GetRandomItemPrefab() : null;
    }

    public void ReturnItem(GameObject prefab, GameObject instance)
    {
        ObjectPoolManager.Instance.Release(prefab, instance);
    }

    public GameObject PlaceItem(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;
        return ObjectPoolManager.Instance.Get(prefab, position, Quaternion.identity);
    }

    public Vector3 GetRandomPositionInBounds()
    {
        Bounds bounds = areaCollider.bounds;
        float randomX = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float randomZ = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
        float startY = bounds.max.y + config.RaycastHeightOffset;
        Vector3 rayStart = new Vector3(randomX, startY, randomZ);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, config.MaxRaycastDistance, config.GroundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + config.SpawnOffset;
        }
        return new Vector3(randomX, bounds.center.y, randomZ) + config.SpawnOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = areaCollider != null ? areaCollider : GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
