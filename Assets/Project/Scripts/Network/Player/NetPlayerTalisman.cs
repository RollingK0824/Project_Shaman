using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using Mirror;
using ProjectShaman.AI;


// 효과 부족 그림 한 장의 전송 형식
// 그림 JSON은 Mirror 문자열 한도 (64KB)를 넘을 수 있기에 GZip byte[] 보내는 방식으로
public struct TalismanRecord
{
    public string instanceId;
    public string customName;
    public string effectId;
    public byte[] drawing;
}


// [클라] 그린 부적 저장 요청을 서버로 전달, [서버] 저장 판정 후 그린 부적 목록을 소유 클라에 추가, 삭제 단위로 전달
// 빈 부적 차감은 NetPlayerInventory쪽에서 자동 동기화
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerTalismanCollection))]
public class NetPlayerTalisman : NetworkBehaviour
{
    private const int MaxCompressedBytes = 256 * 1024;
    private const int MaxDrawingJsonBytes = 600000;

    [SerializeField] private NetItemDataBase _dataBase;

    private PlayerTalismanCollection _collection;
    private PlayerTalismanDrawingController _drawing;
    private NetPlayerStatus _status;

    [Tooltip("효과 ID로 효과 정의를 찾을 효과 부적 목록")]
    [SerializeField] private TalismanItemData[] _effectTalismans;

    private PlayerTalismanEffectController _effects;
    private PlayerVillagerTracking _tracking;
    private NetPlayerEquipment _equipment;
    private long _trackingVersion;
    private long _receivedTrackingVersion = -1;
    private Coroutine _pendingTracking;

    // [서버] 버프 상태 전송 번호 (클라는 이전 번호 무시)
    private long _effectVersion;

    // [서버] 소유 클라에 이미 보낸 그림 ID
    private HashSet<string> _sentIds = new HashSet<string>();

    // [클라] 서버에서 받은 그림 기록(아이템 ID별로) ApplyReplicatedSnapshot에 종류별 전체 목록으로 넘김
    private readonly Dictionary<int, List<TalismanSaveData>> _records = new Dictionary<int, List<TalismanSaveData>>();
    private long _clientVersion;

    private void Awake()
    {
        _collection = GetComponent<PlayerTalismanCollection>();
        _drawing = GetComponent<PlayerTalismanDrawingController>();
        _status = GetComponent<NetPlayerStatus>();
        _effects = GetComponent<PlayerTalismanEffectController>();
        _tracking = _collection.Tracking;
        _equipment = GetComponent<NetPlayerEquipment>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _collection.SetAuthorityCheck(ServerCanAct);
        _collection.SelectionChanged += ServerSyncDrawings;
        _effects.SetAuthorityCheck(ServerCanAct);
        _effects.EffectsChanged += ServerSyncEffects;
        _tracking.StateChanged += ServerSyncTracking;
    }

    public override void OnStopServer()
    {
        _tracking.StateChanged -= ServerSyncTracking;
        _tracking.ResetRoundTracking();
        _trackingVersion = 0;
        _effects.EffectsChanged -= ServerSyncEffects;
        _effects.SetAuthorityCheck(null);
        _collection.SelectionChanged -= ServerSyncDrawings;
        _collection.SetAuthorityCheck(null);

        base.OnStopServer();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _collection.SaveRequested += HandleSaveRequested;
        _collection.UseRequested += HandleUseRequested;
        _tracking.AttachRequested += HandleTrackingRequested;
    }

    public override void OnStopLocalPlayer()
    {
        _tracking.AttachRequested -= HandleTrackingRequested;
        if (_pendingTracking != null) StopCoroutine(_pendingTracking);
        _pendingTracking = null;
        _receivedTrackingVersion = -1;
        _tracking.ResetRoundTracking();
        _collection.UseRequested -= HandleUseRequested;
        _collection.SaveRequested -= HandleSaveRequested;

        base.OnStopLocalPlayer();
    }

    // [서버] 게임 진행 중이고, 살아 있을 때만 
    private bool ServerCanAct()
    {
        if (!NetworkServer.active) return false;
        if (_status != null && _status.IsDead) return false;

        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentGameState == GameState.Ongoing;
    }

    private void Update()
    {
        if (isServer && _tracking.Target != null && !ServerCanAct()) _tracking.ResetTracking();
    }

    // [로컬 클라] 그림 저장 요청을 압축해서 서버로 전달
    private void HandleSaveRequested(TalismanItemData item, TalismanSaveData payload)
    {
        int itemId = _dataBase.GetId(item);
        if (itemId < 0)
        {
            Debug.LogError($"[NetPlayerTalisman] NetItemDataBase에 없는 부적: {item.DisplayName}", this);
            return;
        }

        CmdSave(itemId, Pack(payload));
    }

    // [서버] 압축 해제 후 저장 판정. 그림 검증은 SaveFinal
    [Command]
    private void CmdSave(int itemId, TalismanRecord record)
    {
        bool success = _dataBase.GetItem(itemId) is TalismanItemData item &&
                    Unpack(record) is TalismanSaveData payload &&
                    _collection.SaveFinal(item, payload);

        Debug.Log($"[NetPlayerTalisman] 저장 판정 : {name} -> {record.customName} {(success ? "성공" : "거절")}", this);
        TargetSaveResult(success);
    }

    // [소유 클라] 서버가 저장을 확정하면 그림 창 닫기
    [TargetRpc]
    private void TargetSaveResult(bool success)
    {
        if (success)
        {
            if (_drawing != null) _drawing.Close();
        }
        else
        {
            Debug.LogWarning("[NetPlayerTalisman] 서버가 부적 저장을 거절했습니다.", this);
        }
    }


    // 그린 그림 목록 동기화 작업

    // [서버] 현재목록과 이미 보낸 목록을 비교해서 새로 생긴 그림만 추가, 사라진 그림은 삭제 전송
    // 저장, 사용 후 소모, 초기화 모두 SelectionChanged로 들어옴
    [Server]
    private void ServerSyncDrawings()
    {
        // 호스트 본인은 원본 그대로 사용
        if (isLocalPlayer) return;

        var current = new HashSet<string>();

        foreach(TalismanItemData item in _collection.ItemTypes)
        {
            if (!item.IsEffectTalisman) continue;

            int itemId = _dataBase.GetId(item);
            if (itemId < 0) continue;

            foreach(TalismanSaveData data in _collection.Export(item))
            {
                current.Add(data.InstanceId);
                if (!_sentIds.Contains(data.InstanceId)) TargetAddDrawing(itemId, Pack(data));
            }
        }

        foreach (string id in _sentIds)
        {
            if (!current.Contains(id)) TargetRemoveDrawing(id);
        }

        _sentIds = current;
    }

    [TargetRpc]
    private void TargetAddDrawing(int itemId, TalismanRecord record)
    {
        TalismanSaveData data = Unpack(record);

        if (data == null)
        {
            Debug.LogError($"[NetPlayerTalisman] 그림 기획 해제 실패: {record.instanceId}", this);
            return;
        }

        if (!_records.TryGetValue(itemId, out List<TalismanSaveData> list))
        {
            list = new List<TalismanSaveData>();
            _records.Add(itemId, list);
        }

        list.Add(data);
        ClientApply(itemId);
    }

    [TargetRpc]
    private void TargetRemoveDrawing(string instanceId)
    {
        foreach (KeyValuePair<int, List<TalismanSaveData>> pair in _records)
        {
            if (pair.Value.RemoveAll(data => data.InstanceId == instanceId) > 0)
            {
                ClientApply(pair.Key);
                return;
            }
        }
    }

    // [클라] 모아둔 기록을 종류별 전체 목록으로 반영
    private void ClientApply(int itemId)
    {
        if (!(_dataBase.GetItem(itemId) is TalismanItemData item)) return;

        if (!_collection.ApplyReplicatedSnapshot(++_clientVersion, item, _records[itemId]))
        {
            Debug.LogWarning($"[NetPlayerTalisman] 그린 부적 목록 반영 실패: {item.DisplayName}", this);
        }
    }

    // 사용 버프 상태

    // [로컬 클라] 그린 부적 사용 요청을 서버로 전달
    // 클라의 그림 ID는 서버가 보내준 기록 그대로라 서버와 같음
    private void HandleUseRequested(string instanceId)
    {
        CmdUse(instanceId);
    }

    // [서버] 버프 적용, 그림 소모, 퀘스트 Use 보고는 TryUse가 처리
    // 결과는 ServerSyncDrawings(소모), ServerSyncEffects(버프)가 소유 클라에 자동 전달
    [Command]
    private void CmdUse(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || instanceId.Length > 64) return;

        bool success = _collection.TryUse(instanceId);
        Debug.Log($"[NetPlayerTalisman] 사용 판정 : {name} -> {instanceId} {(success ? "성공" : "거절")}", this);
    }

    private void HandleTrackingRequested(string instanceId, AI_Core target, Vector3 origin, Vector3 direction)
    {
        if (_equipment == null || target == null || !target.TryGetComponent(out NetworkIdentity identity) || identity.netId == 0) return;
        // Send equipment first, including when selection and use occur in the same frame.
        _equipment.SyncEquippedItem(true);
        CmdUseTracking(instanceId, identity.netId, origin, direction);
    }

    [Command]
    private void CmdUseTracking(string instanceId, uint targetId, Vector3 origin, Vector3 direction)
    {
        if (!ServerCanAct() || string.IsNullOrEmpty(instanceId) || instanceId.Length > 64 ||
            !NetworkServer.spawned.TryGetValue(targetId, out NetworkIdentity identity) ||
            !identity.TryGetComponent(out AI_Core target)) return;
        bool success = _collection.TryUseTracking(instanceId, target, origin, direction);
        if (!success) TargetTrackingRejected();
    }

    [TargetRpc]
    private void TargetTrackingRejected()
    {
        GetComponent<PlayerItemFeedback>()?.Unavailable("추적 부적 사용이 거절되었습니다");
    }

    [Server]
    private void ServerSyncTracking()
    {
        if (isLocalPlayer) return; // Host uses the authoritative state directly.
        uint targetId = 0;
        if (_tracking.Target != null && _tracking.Target.TryGetComponent(out NetworkIdentity identity)) targetId = identity.netId;
        TargetTrackingState(++_trackingVersion, targetId, targetId == 0 ? 0 : _tracking.ExpiresAt);
    }

    [TargetRpc]
    private void TargetTrackingState(long version, uint targetId, double expiresAt)
    {
        if (isServer || version <= _receivedTrackingVersion) return;
        _receivedTrackingVersion = version;
        if (_pendingTracking != null) StopCoroutine(_pendingTracking);
        _tracking.ResetTracking();
        _pendingTracking = StartCoroutine(ApplyTrackingWhenReady(version, targetId, expiresAt));
    }

    private IEnumerator ApplyTrackingWhenReady(long version, uint targetId, double expiresAt)
    {
        while (isLocalPlayer && version == _receivedTrackingVersion)
        {
            if (targetId == 0)
            {
                _tracking.ApplyConfirmed(version, null, 0);
                break;
            }
            if (NetworkClient.spawned.TryGetValue(targetId, out NetworkIdentity identity) &&
                identity.TryGetComponent(out AI_Core target) && _tracking.ApplyConfirmed(version, target, expiresAt)) break;
            if (TimeManager.Existing != null && TimeManager.Existing.IsRunning && TimeManager.Existing.Elapsed >= expiresAt) break;
            yield return null;
        }
        _pendingTracking = null;
    }


    // [서버] 버프가 적용, 만료, 초기화될 때마다 남은 시간 목록을 소유 클라에 전달
    [Server]
    private void ServerSyncEffects()
    {
        if (isLocalPlayer) return;

        // ExportState 안에서 만료 처리가 일어나면 이 함수가 한 번 더 불림
        // 상태를 먼저 꺼낸 뒤 번호를 붙여야 나중에 보낸 쪽이 더 큰 번호가 됨
        TalismanEffectState[] states = _effects.ExportState();
        TargetEffects(++_effectVersion, states);
    }

    [TargetRpc]
    private void TargetEffects(long version, TalismanEffectState[] states)
    {
        // 사망 상태에서는 ApplyReplicatedState가 거절함 (사망 시 버프는 클라에서도 따로 초기화됨)
        if (!_effects.ApplyReplicatedState(version, states, FindEffect) && PlayerActionGuard.CanAct(gameObject))
        {
            Debug.LogWarning($"[NetPlayerTalisman] 버프 상태 반영 실패 (version {version})", this);
        }
    }

    private TalismanEffectDefinition FindEffect(string effectId)
    {
        if (_effectTalismans == null) return null;

        foreach (TalismanItemData talisman in _effectTalismans)
        {
            TalismanEffectDefinition effect = talisman != null ? talisman.FindEffect(effectId) : null;
            if (effect != null) return effect;
        }

        return null;
    }

    private static TalismanRecord Pack(TalismanSaveData data)
    {
        byte[] raw = Encoding.UTF8.GetBytes(data.DrawingJson ?? string.Empty);

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            gzip.Write(raw, 0, raw.Length);
        }

        return new TalismanRecord
        {
            instanceId = data.InstanceId,
            customName = data.CustomName,
            effectId = data.EffectId,
            drawing = output.ToArray()
        };
    }

    private static TalismanSaveData Unpack(TalismanRecord record)
    {
        if (record.drawing == null || record.drawing.Length == 0 || record.drawing.Length > MaxCompressedBytes) return null;

        try
        {
            using var input = new MemoryStream(record.drawing);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            var buffer = new byte[8192];
            int read;
            while((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxDrawingJsonBytes) return null;
                output.Write(buffer, 0, read);
            }

            return new TalismanSaveData
            {
                InstanceId = record.instanceId,
                CustomName = record.customName,
                EffectId = record.effectId,
                DrawingJson = Encoding.UTF8.GetString(output.ToArray())
            };

        }
        catch(InvalidDataException)
        {
            return null;
        }
    }
}
