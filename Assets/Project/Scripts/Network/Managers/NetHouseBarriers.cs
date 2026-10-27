using UnityEngine;
using Mirror;
using System.Collections.Generic;
using ProjectShaman.AI.World;

// 결계 부적 집 부착 (우클릭) - 씬 매니저
// [서버] 진짜 HouseBarrierWard(보호 판정)는 서버에만 존재. 성공한 부착만 목록에 공개, 만료, 라운드 종료 때 목록에서 뺌 
// [모든 클라] 목록대로 부적 모습만 만듦
// [호스트] 서버의 진짜 ward가 이미 보이므로 모습을 따로 만들지 않음
// 붙인 플레이어가 나가도 부적은 남아야 하므로 플레이어가 아니라 씬 오브젝트에 둠
public struct HouseBarrierEntry
{
    public int wardId;              // 서버가 매기는 번호
    public string homeId;           // PlayerArea.PlaceId
    public int talismanId;          // NetItemDataBase 번호
    public Vector3 position;        // 서버 ward의 실제 위치, 회전
    public Quaternion rotation;
}

[RequireComponent(typeof(NetworkIdentity))]
public class NetHouseBarriers : NetworkBehaviour
{
    public static NetHouseBarriers Instance { get; private set; }

    [SerializeField] private NetItemDataBase _dataBase;

    private readonly SyncList<HouseBarrierEntry> _entries = new SyncList<HouseBarrierEntry>();

    private readonly Dictionary<HouseBarrierWard, int> _serverIds = new Dictionary<HouseBarrierWard, int>();
    private readonly Dictionary<int, HouseBarrierWard> _clientWards = new Dictionary<int, HouseBarrierWard>(); // [클라] 모습만


    private int _nextWardId;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // [클라] 우클릭 전에 미리 막기용. 클라에는 진짜 ward가 없어 플레이어 쪽 중복 확인이 통과됨
    public bool IsHomeProtected(string homeId)
    {
        foreach (HouseBarrierEntry entry in _entries)
            if (entry.homeId == homeId) return true;

        return false;
    }


    // 서버

    // [서버] TryPlaceAuthoritative가 true를 반환한 뒤에만 호출
    [Server]
    public void ServerPublish(HouseBarrierWard ward, int talismanId)
    {
        if (ward == null || _serverIds.ContainsKey(ward)) return;

        int id = ++_nextWardId;
        _serverIds[ward] = id;
        _entries.Add(new HouseBarrierEntry
        {
            wardId = id,
            homeId = ward.HomeId,
            talismanId = talismanId,
            position = ward.transform.position,
            rotation = ward.transform.rotation,
        });
    }

    // [서버] TryPlaceAuthoritative의 despawn 콜백. 콜백과 만료 모두 여기로 전달
    [Server]
    public void ServerDespawn(HouseBarrierWard ward)
    {
        if (ward != null && _serverIds.TryGetValue(ward, out int id))
        {
            _serverIds.Remove(ward);
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].wardId == id) _entries.RemoveAt(i);
        }

        if (ward != null) Destroy(ward.gameObject);
    }

    // [서버] 라운드가 끝나면 남은 결계 전부 해제
    private void Update()
    {
        if (!isServer || _serverIds.Count == 0) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) ServerExpireAll();
    }

    public override void OnStopServer()
    {
        ServerExpireAll();
        base.OnStopServer();
    }

    [Server]
    private void ServerExpireAll()
    {
        // Expire -> ServerDespawn이 _serverIds를 바꾸므로 복사본으로 둚
        foreach (HouseBarrierWard ward in new List<HouseBarrierWard>(_serverIds.Keys))
            if (ward != null) ward.Expire();

        _serverIds.Clear();
        _entries.Clear();
    }


    // 클라
    public override void OnStartClient()
    {
        base.OnStartClient();
        _entries.OnAdd += HandleEntryAdded;
        _entries.OnRemove += HandleEntryRemoved;

        foreach (HouseBarrierEntry entry in _entries) ShowWard(entry);
    }
    public override void OnStopClient()
    {
        _entries.OnAdd -= HandleEntryAdded;
        _entries.OnRemove -= HandleEntryRemoved;

        foreach (HouseBarrierWard ward in _clientWards.Values)
            if (ward != null) ward.Expire();
        _clientWards.Clear();

        base.OnStopClient();
    }


    private void HandleEntryAdded(int index) => ShowWard(_entries[index]);
    private void HandleEntryRemoved(int index, HouseBarrierEntry removed) => HideWard(removed.wardId);

    private void ShowWard(HouseBarrierEntry entry)
    {
        if (isServer || _clientWards.ContainsKey(entry.wardId)) return;   // 호스트는 진짜 ward가 보임
        if (!PlaceRegistry.TryGet(entry.homeId, out PlaceArea home)) return;

        TalismanItemData data = _dataBase.GetItem(entry.talismanId) as TalismanItemData;
        HouseBarrierWard ward = new GameObject("HouseBarrierView_" + entry.homeId).AddComponent<HouseBarrierWard>();

        // Prepare만 하고 Arm은 하지 않음 → AI 보호 등록·자동 만료 없이 모습만
        ward.Prepare(home, entry.position, entry.rotation * Vector3.forward, data != null ? data.PaperTexture : null, null);
        ward.transform.SetPositionAndRotation(entry.position, entry.rotation);   // 서버와 똑같은 위치로 맞춤

        _clientWards[entry.wardId] = ward;
    }

    private void HideWard(int wardId)
    {
        if (!_clientWards.TryGetValue(wardId, out HouseBarrierWard ward)) return;

        _clientWards.Remove(wardId);
        if (ward != null) ward.Expire();   // despawn 콜백이 null이라 Destroy로 정리됨
    }
}
