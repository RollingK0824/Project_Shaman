using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WeightedSpawnItem
{
    public GameObject itemPrefab;
    [Min(0f)] public float weight;   // 0이면 스폰되지 않음. 다른 아이템 대비 상대값
}

// 스포너 공용 설정. 같은 SO를 여러 스포너가 공유할 수 있습니다.
[CreateAssetMenu(fileName = "ItemSpawnerConfig", menuName = "Project Shaman/Item Spawner Config")]
public class ItemSpawnerConfig : ScriptableObject
{
    [Header("Item Pool")]
    [SerializeField] private List<WeightedSpawnItem> items = new List<WeightedSpawnItem>();

    [Header("Auto Spawn")]
    [SerializeField] private bool autoSpawn = true;
    [SerializeField, Min(0.1f)] private float minSpawnInterval = 30f;
    [SerializeField, Min(0.1f)] private float maxSpawnInterval = 60f;
    [SerializeField, Min(1)] private int maxActiveItems = 3;

    [Header("Placement")]
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float raycastHeightOffset = 5f;
    [SerializeField] private float maxRaycastDistance = 15f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);

    public bool AutoSpawn => autoSpawn;
    public int MaxActiveItems => maxActiveItems;
    public LayerMask GroundLayer => groundLayer;
    public float RaycastHeightOffset => raycastHeightOffset;
    public float MaxRaycastDistance => maxRaycastDistance;
    public Vector3 SpawnOffset => spawnOffset;

    public bool HasSpawnableItems
    {
        get
        {
            foreach (WeightedSpawnItem item in items)
            {
                if (item.itemPrefab != null && item.weight > 0f) return true;
            }
            return false;
        }
    }

    public float GetRandomInterval()
    {
        return UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    // 가중치 비례 랜덤 선택. 스폰 가능한 아이템이 없으면 null
    public GameObject GetRandomItemPrefab()
    {
        float totalWeight = 0f;
        foreach (WeightedSpawnItem item in items)
        {
            if (item.itemPrefab != null && item.weight > 0f) totalWeight += item.weight;
        }
        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        GameObject last = null;
        foreach (WeightedSpawnItem item in items)
        {
            if (item.itemPrefab == null || item.weight <= 0f) continue;

            last = item.itemPrefab;
            roll -= item.weight;
            if (roll < 0f) return item.itemPrefab;
        }

        // 부동소수 오차로 끝까지 온 경우
        return last;
    }

    private void OnValidate()
    {
        if (maxSpawnInterval < minSpawnInterval) maxSpawnInterval = minSpawnInterval;
    }
}
