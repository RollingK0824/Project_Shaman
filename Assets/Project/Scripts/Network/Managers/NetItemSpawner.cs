using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 스폰 요청은 서버의 시스템 로직에서 호출합니다. 클라이언트용 Command는 제공하지 않습니다.
// 스폰 간격, 최대 개수, 아이템 가중치는 ItemSpawner의 ItemSpawnerConfig(SO)에서 설정합니다.
[RequireComponent(typeof(ItemSpawner))]
public sealed class NetItemSpawner : MonoBehaviour
{
    private ItemSpawner _spawner;
    // 스폰된 인스턴스 -> 원본 프리팹 (풀 반환용)
    private readonly Dictionary<GameObject, GameObject> _spawnedItems = new Dictionary<GameObject, GameObject>();
    private float _spawnTimer;

    private static readonly List<NetItemSpawner> _activeSpawners = new List<NetItemSpawner>();

    public static IReadOnlyList<NetItemSpawner> ActiveSpawners => _activeSpawners;
    public int SpawnedCount => _spawnedItems.Count;

    private void Awake()
    {
        _spawner = GetComponent<ItemSpawner>();
    }

    private void OnEnable()
    {
        _spawner.SpawnRequested += HandleSpawnRequested;
        _activeSpawners.Add(this);
        ResetSpawnTimer();
    }

    private void OnDisable()
    {
        _spawner.SpawnRequested -= HandleSpawnRequested;
        _activeSpawners.Remove(this);
    }

    // 서버에서만 일정 간격(랜덤)으로 스폰. 최대 개수에 도달하면 아이템이 회수될 때까지 대기
    private void Update()
    {
        ItemSpawnerConfig config = _spawner.Config;
        if (config == null || !config.AutoSpawn || !NetworkServer.active) return;

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        ResetSpawnTimer();
        if (SpawnedCount >= config.MaxActiveItems) return;

        _spawner.RequestSpawn();
    }

    private void ResetSpawnTimer()
    {
        ItemSpawnerConfig config = _spawner.Config;
        _spawnTimer = config != null ? config.GetRandomInterval() : 0f;
    }

    private void HandleSpawnRequested()
    {
        ServerSpawnRandom();
    }

    public GameObject ServerSpawnRandom()
    {
        if (!NetworkServer.active || !isActiveAndEnabled) return null;

        GameObject prefab = _spawner.GetRandomItemPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[NetItemSpawner] 스폰할 아이템이 없습니다.", this);
            return null;
        }

        if (!prefab.TryGetComponent(out NetworkIdentity prefabIdentity)
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

        Vector3 position = _spawner.GetRandomPositionInBounds();
        GameObject item = _spawner.PlaceItem(prefab, position);
        if (item == null) return null;

        NetworkServer.Spawn(item);
        NetworkIdentity identity = item.GetComponent<NetworkIdentity>();
        if (identity.netId == 0)
        {
            _spawner.ReturnItem(prefab, item);
            return null;
        }

        _spawnedItems.Add(item, prefab);
        Debug.Log($"[NetItemSpawner] 스폰: {prefab.name}, netId={identity.netId}, position={position}, count={SpawnedCount}", item);
        return item;
    }

    public bool ServerDespawn(GameObject item)
    {
        if (!NetworkServer.active || item == null || !_spawnedItems.TryGetValue(item, out GameObject prefab))
            return false;

        // 클라이언트에는 제거를 전송하고 서버 오브젝트는 초기화 후 풀에 반환합니다.
        NetworkServer.UnSpawn(item);
        _spawnedItems.Remove(item);
        _spawner.ReturnItem(prefab, item);
        Debug.Log($"[NetItemSpawner] 회수: count={SpawnedCount}", this);
        return true;
    }


    // 해당 아이템을 스폰한 스포너를 찾아서 회수.. 따로 Spawner가 없다면 false로 반환
    public static bool ServerDespawnFromAny(GameObject item)
    {
        foreach(NetItemSpawner spawner in _activeSpawners)
        {
            if (spawner.ServerDespawn(item)) return true;
        }

        return false;
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
