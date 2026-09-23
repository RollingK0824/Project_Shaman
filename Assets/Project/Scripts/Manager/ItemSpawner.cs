using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SpawnerItemData
{
    public GameObject itemPrefab;      
}

[RequireComponent(typeof(Collider))]
public class ItemSpawner : MonoBehaviour
{
    [Header("Item Pool Configuration")]
    [SerializeField] private List<SpawnerItemData> itemPool = new List<SpawnerItemData>();
    [Header("Spawn Area Settings")]
    [SerializeField] private Collider areaCollider;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastHeightOffset = 5f;
    [SerializeField] private float maxRaycastDistance = 15f;
    private void Awake()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<Collider>();
        }
    }

    public void RequestSpawn()
    {
        if (itemPool == null || itemPool.Count == 0)
        {
            Debug.LogWarning($"[RandomItemSpawner] No items in pool on {gameObject.name}");
            return;
        }
        int selectedItemIndex = GetRandomItemIndex();
        Vector3 spawnPosition = GetRandomPositionInBounds();
        Debug.Log($"[RandomItemSpawner] Requesting Networker approval for item index {selectedItemIndex} at {spawnPosition}");
    }

    public GameObject PlaceItem(int itemIndex, Vector3 position)
    {
        if (itemIndex < 0 || itemIndex >= itemPool.Count) return null;
        SpawnerItemData itemToSpawn = itemPool[itemIndex];
        if (itemToSpawn.itemPrefab == null) return null;
        GameObject spawnedObj = ObjectPoolManager.Instance.Get(
            itemToSpawn.itemPrefab,
            position,
            Quaternion.identity
        );
        return spawnedObj;
    }

    public int GetRandomItemIndex()
    {
        return UnityEngine.Random.Range(0, itemPool.Count);
    }
    public Vector3 GetRandomPositionInBounds()
    {
        Bounds bounds = areaCollider.bounds;
        float randomX = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float randomZ = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
        float startY = bounds.max.y + raycastHeightOffset;
        Vector3 rayStart = new Vector3(randomX, startY, randomZ);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer))
        {
            return hit.point;
        }
        return new Vector3(randomX, bounds.center.y, randomZ);
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