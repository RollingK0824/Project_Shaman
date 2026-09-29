using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 스폰 요청은 서버의 시스템 로직에서 호출합니다. 클라이언트용 Command는 제공하지 않습니다.
[RequireComponent(typeof(ItemSpawner))]
public sealed class NetItemSpawner : MonoBehaviour
{
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 0.5f, 0f);

    private ItemSpawner _spawner;
    private readonly Dictionary<GameObject, int> _spawnedItems = new Dictionary<GameObject, int>();

    public int SpawnedCount => _spawnedItems.Count;

    private void Awake()
    {
        _spawner = GetComponent<ItemSpawner>();
    }

    private void OnEnable()
    {
        _spawner.SpawnRequested += HandleSpawnRequested;
    }

    private void OnDisable()
    {
        _spawner.SpawnRequested -= HandleSpawnRequested;
    }

    private void HandleSpawnRequested()
    {
        ServerSpawnRandom();
    }

    public GameObject ServerSpawnRandom()
    {
        if (!NetworkServer.active || !isActiveAndEnabled) return null;

        if (_spawner.ItemCount == 0)
        {
            Debug.LogWarning("[NetItemSpawner] 스폰할 아이템이 없습니다.", this);
            return null;
        }

        int itemIndex = _spawner.GetRandomItemIndex();
        GameObject prefab = _spawner.GetItemPrefab(itemIndex);
        if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity prefabIdentity)
            || prefabIdentity.assetId == 0 || prefabIdentity.sceneId != 0)
        {
            Debug.LogError("[NetItemSpawner] 아이템은 루트에 NetworkIdentity가 있는 프리팹이어야 합니다.", this);
            return null;
        }

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null || !manager.spawnPrefabs.Contains(prefab))
        {
            Debug.LogError($"[NetItemSpawner] {prefab.name}을 NetworkManager의 Spawnable Prefabs에 등록하세요.", this);
            return null;
        }

        Vector3 position = _spawner.GetRandomPositionInBounds() + _spawnOffset;
        GameObject item = _spawner.PlaceItem(itemIndex, position);
        if (item == null) return null;

        NetworkServer.Spawn(item);
        NetworkIdentity identity = item.GetComponent<NetworkIdentity>();
        if (identity.netId == 0)
        {
            _spawner.ReturnItem(itemIndex, item);
            return null;
        }

        _spawnedItems.Add(item, itemIndex);
        Debug.Log($"[NetItemSpawner] 스폰: {prefab.name}, netId={identity.netId}, position={position}, count={SpawnedCount}", item);
        return item;
    }

    public bool ServerDespawn(GameObject item)
    {
        if (!NetworkServer.active || item == null || !_spawnedItems.TryGetValue(item, out int itemIndex))
            return false;

        // 클라이언트에는 제거를 전송하고 서버 오브젝트는 초기화 후 풀에 반환합니다.
        NetworkServer.UnSpawn(item);
        _spawnedItems.Remove(item);
        _spawner.ReturnItem(itemIndex, item);
        Debug.Log($"[NetItemSpawner] 회수: count={SpawnedCount}", this);
        return true;
    }

    public void ServerDespawnAll()
    {
        if (!NetworkServer.active) return;

        foreach (GameObject item in new List<GameObject>(_spawnedItems.Keys))
        {
            if (item != null) ServerDespawn(item);
            else _spawnedItems.Remove(item);
        }
    }

    private void OnDestroy()
    {
        // 씬 종료 시 활성 아이템은 Mirror/Unity가 정리합니다. 종료 중 풀을 새로 만들지 않습니다.
        _spawnedItems.Clear();
    }
}
