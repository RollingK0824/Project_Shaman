using UnityEngine;
using Mirror;
using System.Collections.Generic;

// [서버] 공용 재료 원본은 보관, 바뀔때마다 스냅샷 발행
// [클라] 스냅샷을 받아 읽기 전용으로 반영
// 재료만 공용, 사용 아이템은 개인 목록에
public struct SharedMaterialSnapshot
{
    public long version;
    public int[] ids;
    public int[] counts;
}


[RequireComponent(typeof(NetworkIdentity), typeof(SharedMaterialInventory))]
public class NetSharedMaterials : NetworkBehaviour
{
    public static NetSharedMaterials Instance { get; private set; }

    [SerializeField] private NetItemDataBase _dataBase;

    [SyncVar(hook = nameof(OnSnapshotChanged))]
    private SharedMaterialSnapshot _snapshot;

    private SharedMaterialInventory _store;
    public SharedMaterialInventory Store => _store;

    private void Awake()
    {
        _store = GetComponent<SharedMaterialInventory>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _store.SetAuthorityCheck(() => NetworkServer.active);
        _store.Changed += ServerPublish;
        BindAllPlayers();
    }

    public override void OnStopServer()
    {
        _store.Changed -= ServerPublish;
        _store.SetAuthorityCheck(null);


        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 스폰 시점 값은 hook이 안 불릴 수 있기에 직접 한번 실행
        ApplySnapshot(_snapshot);
        BindAllPlayers();
    }

    public override void OnStopClient()
    {
        if (Instance == this) Instance = null;

        base.OnStopClient();
    }

    // 플레이어와 이 오브젝트의 스폰 순서가 정해져 있지 않음
    // 여기서 이미 있는 플레이어를 묶고 나중에 스폰된 플레이어는 NetPlayerInventory에서 Instance를 찾아서 묶음
    private void BindAllPlayers()
    {
        Instance = this;
        foreach(PlayerInventory inventory in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
        {
            inventory.BindSharedMaterials(_store);
        }
    }

    // [서버] 원본이 바뀔때마다 ItemData를 ID로 바꿔 전체 스냅샷 발행
    [Server]
    private void ServerPublish()
    {
        Dictionary<ItemData, int> counts = _store.ExportSnapshot(out long version);

        var ids = new List<int>(counts.Count);
        var values = new List<int>(counts.Count);

        foreach(KeyValuePair<ItemData, int> pair in counts)
        {
            int id = _dataBase.GetId(pair.Key);
            if (id < 0)
            {
                Debug.LogError($"[NetSharedMaterials] NetItemDataBase에 없는 재료: {pair.Key.DisplayName}", this);
                continue;
            }

            ids.Add(id);
            values.Add(pair.Value);
        }

        _snapshot = new SharedMaterialSnapshot { version = version, ids = ids.ToArray(), counts = values.ToArray() };
    }

    private void OnSnapshotChanged(SharedMaterialSnapshot oldValue, SharedMaterialSnapshot newValue)
    {
        ApplySnapshot(newValue);
    }

    private void ApplySnapshot(SharedMaterialSnapshot snapshot)
    {
        if (isServer || snapshot.ids == null || snapshot.version <= _store.Version) return;

        var counts = new Dictionary<ItemData, int>();
        for (int i = 0; i < snapshot.ids.Length; i++)
        {
            ItemData item = _dataBase.GetItem(snapshot.ids[i]);
            if (item == null)
            {
                Debug.LogError($"[NetSharedMaterials] 알 수 없는 재료 ID: {snapshot.ids[i]}", this);
                return;
            }

            counts[item] = snapshot.counts[i];
        }

        if (!_store.ApplyReplicatedSnapshot(snapshot.version, counts))
        {
            Debug.LogWarning($"[NetSharedMaterials] 스냅샷 반영 실패 (version {snapshot.version})", this);
        }
    }


#if UNITY_EDITOR
    // [테스트 전용] 재료 줍기(C 작업) 전에 서버에서 직접 재료를 넣어 동기화 확인
    [SerializeField] private ItemData _testMaterial;

    [ContextMenu("Test/Add Test Material (Server)")]
    private void TestAddMaterial()
    {
        if (!isServer || PlayerRoster.Players.Count == 0)
        {
            Debug.LogWarning("[NetSharedMaterials] 호스트에서, 플레이어 스폰 뒤 실행해주세요", this);
            return;
        }

        _store.TryAdd(PlayerRoster.Players[0].gameObject, _testMaterial, 1);
    }
#endif
}
