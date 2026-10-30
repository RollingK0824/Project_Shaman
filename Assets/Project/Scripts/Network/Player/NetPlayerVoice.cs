using UnityEngine;
using System;
using Mirror;
using Steamworks;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// [로컬 클라] Steam 마이크로 녹음한 압축 음성을 서버로 보냄 (말할 때만 데이터가 나옴)
// [서버] 듣는 사람마다 채널을 골라 전달 (근거리 / 방울 무전 / 관전자)
// [받는 클라] 압축을 풀어서 말한 플레이어 쪽 VoicePlayback으로 재생
[RequireComponent(typeof(NetworkIdentity ))]
public class NetPlayerVoice : NetworkBehaviour
{
    public enum VoiceChannel : byte { Proximity, Radio, Spectator}

    // SteamTransport 비신뢰 한도 (1200)보다 여유롭게
    private const int MaxPacketBytes = 1000;

    [Header("Rule")]
    [SerializeField] private ItemData _bellData;
    [SerializeField, Min(1f)] private float _proximityRange = 25f;

    [Header("Playback")]
    [SerializeField] private VoicePlayback _proximityPlayback;
    [SerializeField] private VoicePlayback _radioPlayback;

    [Header("Radio Cue")]
    [SerializeField] private AudioClip _radioStartClip;
    [SerializeField] private AudioClip _radioEndClip;
    [SerializeField, Range(0f, 1f)] private float _radioCueVolume = .6f;

    [Header("Debug")]
    [Tooltip("로컬 클라에서 내 목소리를 바로 들어 보는 테스트용")]
    [SerializeField] private bool _debugLoopback;


    // [로컬 클라] 방울 무전 키. 플레이어 입력 에셋 (ShamanInput)과 별도로 관리
    private InputAction _radioAction;
    private bool _ownsRadioAction;

    private NetPlayerStatus _status;
    private NetPlayerEquipment _equipment;
    private PlayerInventory _inventory;

    private readonly byte[] _recordBuffer = new byte[8 * 1024];
    private byte[] _decompressBuffer;
    private uint _sampleRate;
    private bool _recording;

    private bool _serverBellInQuickSlot;
    private bool _lastSentBellInQuickSlot;

    // [로컬 클라] 직전 무전 상태. 바뀌는 순간에만 신호음 요청
    private bool _wasTalkingOnRadio;

    // [서버] 시작 신호를 보낸 무전이 진행중인지 (끝 신호를 짝 맞춰 보내기 위함)
    private bool _serverRadioActive;

    // [모든 클라] 게임 씬의 플레이어 음성 목록. 플레이어별 음소거 UI용 (PlayerRoster는 서버에만 있음)
    private static readonly List<NetPlayerVoice> _all = new List<NetPlayerVoice>();
    public static IReadOnlyList<NetPlayerVoice> All => _all;
    public static event Action ListChanged;

    private float _lastVoiceTime = -1f;

    public string DisplayName => _status != null ? _status.PlayerName : name;
    public bool IsLocal => isLocalPlayer;
    // [로컬 클라] 이 플레이어 목소리를 이 PC에서만 끔. 그 판에만 적용 (저장 안 함)
    public bool IsMutedLocally { get; private set; }
    public event Action<bool> MutedLocallyChanged;
    // 최근 0.25초 안에 음성이 오갔는지 (말하는 중 아이콘용)
    public bool IsSpeaking => Time.time - _lastVoiceTime < .25f;

    public void SetMutedLocally(bool muted)
    {
        if (IsLocal || IsMutedLocally == muted) return;

        IsMutedLocally = muted;
        MutedLocallyChanged?.Invoke(muted);
    }

    // Enter Play Mode에서 도메인 리로드를 꺼 둔 경우 대비
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
        ListChanged = null;
    }

    // [로컬 클라] HUD에서 "무전 중" 표시할 때 사용
    public bool IsTalkingOnRadio { get; private set; }

    private bool IsDead => _status != null && _status.IsDead;
    private bool IsHoldingBell => _bellData != null && _equipment.EquippedItem == _bellData;

    // [서버] 듣는 쪽 무전 수신 조건: 생존 + 방울 퀵슬롯 등록 + 실제 보유
    private bool ServerCanHearRadio => !IsDead && _serverBellInQuickSlot && _bellData != null && _inventory.Contains(_bellData);

    private void Awake()
    {
        _status = GetComponent<NetPlayerStatus>();
        _equipment = GetComponent<NetPlayerEquipment>();
        _inventory = GetComponent<PlayerInventory>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _all.Add(this);
        ListChanged?.Invoke();

        if (!SteamManager.Initialized) return;

        _sampleRate = SteamUser.GetVoiceOptimalSampleRate();
        _decompressBuffer = new byte[_sampleRate * 2]; // 16비트 모노 1초 분량
        _proximityPlayback.Init((int)_sampleRate);
        _radioPlayback.Init((int)_sampleRate);
    }

    public override void OnStopClient()
    {
        _all.Remove(this);
        ListChanged?.Invoke();

        base.OnStopClient();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[NetPlayerVoice] Steam이 초기화되지 않아 음성 채팅을 끕니다.", this);
            return;
        }

        // ShamanInput에 Player/VoiceRadio가 있으면 공용 액션(키 재설정 가능), 없으면 B 키 임시 액션
        _radioAction = InputManager.Instance.Actions.asset.FindAction("Player/VoiceRadio", false);
        _ownsRadioAction = _radioAction == null;
        if (_ownsRadioAction)
        {
            _radioAction = new InputAction("VoiceRadio", InputActionType.Button, "<Keyboard>/b");
            _radioAction.Enable();
        }
        SteamUser.StartVoiceRecording();
        _recording = true;
        _inventory.QuickSlotChanged += HandleQuickSlotChanged;
        SyncBellQuickSlot(); // 시작할 때 이미 등록돼 있는 경우
    }

    public override void OnStopLocalPlayer()
    {
        if (_recording)
        {
            SteamUser.StopVoiceRecording();
            _recording = false;
        }

        if (_ownsRadioAction) _radioAction?.Dispose();
        _ownsRadioAction = false;
        _radioAction = null;
        _inventory.QuickSlotChanged -= HandleQuickSlotChanged;
        base.OnStopLocalPlayer();
    }


    // [로컬 클라] 녹음된만큼 꺼내서 서버로
    private void Update()
    {
        if (!_recording) return;

        IsTalkingOnRadio = !VoiceSettings.MicMuted && !IsDead && IsHoldingBell && _radioAction != null &&_radioAction.IsPressed() && !InputManager.Instance.IsRebinding;

        if (IsTalkingOnRadio != _wasTalkingOnRadio)
        {
            _wasTalkingOnRadio = IsTalkingOnRadio;
            CmdSetRadioActive(IsTalkingOnRadio);
            // 로컬도 들어서 무전이 켜지고 꺼진 걸 알 수 있게
            _radioPlayback.PlayCue(IsTalkingOnRadio ? _radioStartClip : _radioEndClip, _radioCueVolume);
        }

        if (SteamUser.GetAvailableVoice(out uint available) != EVoiceResult.k_EVoiceResultOK || available == 0) return;
        if (SteamUser.GetVoice(true, _recordBuffer, (uint)_recordBuffer.Length, out uint written) != EVoiceResult.k_EVoiceResultOK || written == 0) return;

        // 음소거 중에도 Steam 버퍼는 비워 둠 (풀었을 때 밀린 음성이 한꺼번에 나가지 않게)
        if (VoiceSettings.MicMuted) return;

        // 프레임 크게 밀려 한 번에 너무 많이 쌓였으면 버림 (비신뢰 한도 초과)
        if (written > MaxPacketBytes) return;

        byte[] packet = new byte[written];
        Buffer.BlockCopy(_recordBuffer, 0, packet, 0, (int)written);

        _lastVoiceTime = Time.time;
        CmdSendVoice(packet, IsTalkingOnRadio);

        if (_debugLoopback) PlayVoice(packet, IsTalkingOnRadio ? VoiceChannel.Radio : VoiceChannel.Spectator);
    }

    // [로컬 클라] 퀵슬롯이 바뀔 때마다 다시 계산
    private void HandleQuickSlotChanged(int slotIndex, ItemData item) => SyncBellQuickSlot();

    private void SyncBellQuickSlot()
    {
        bool registered = IsBellInQuickSlot();
        if (registered == _lastSentBellInQuickSlot) return;

        _lastSentBellInQuickSlot = registered;
        CmdSetBellInQuickSlot(registered);
    }

    private bool IsBellInQuickSlot()
    {
        if (_bellData == null) return false;

        for (int i = 0; i < _inventory.QuickSlotCount; i++)
        {
            if (_inventory.GetQuickSlotItem(i) == _bellData) return true;
        }
        return false;
    }

    // [서버] 실제 보유 여부는 무전 판정 때 Contains로 다시 확인
    [Command]
    private void CmdSetBellInQuickSlot(bool registered) => _serverBellInQuickSlot = registered;

    // [서버] 무전 시작/끝. 시작은 살아 있고 방울을 들고 있을 때만 인정
    [Command]
    private void CmdSetRadioActive(bool active)
    {
        if (active && (IsDead || !IsHoldingBell)) return;
        if (active == _serverRadioActive) return;

        _serverRadioActive = active;

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == connectionToClient || conn.identity == null) continue;
            if (!conn.identity.TryGetComponent(out NetPlayerVoice listener)) continue;

            if (listener.ServerCanHearRadio) TargetPlayRadioCue(conn, active);
        }
    }

    // [받는 클라] 말한 플레이어 쪽 VoiceRadio에서 재생
    [TargetRpc]
    private void TargetPlayRadioCue(NetworkConnectionToClient target, bool start)
    {
        if (IsMutedLocally) return;
        _radioPlayback.PlayCue(start ? _radioStartClip : _radioEndClip, _radioCueVolume);
    }

    // [서버] 듣는 사람마다 채널을 골라 전달. 클라가 보낸 무전 여부는 서버가 다시 확인
    [Command(channel = Channels.Unreliable)]
    private void CmdSendVoice(byte[] data, bool wantsRadio)
    {
        if (data == null || data.Length == 0 || data.Length > MaxPacketBytes) return;

        bool speakerDead = IsDead;
        bool radio = wantsRadio && !speakerDead && IsHoldingBell;

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == connectionToClient || conn.identity == null) continue;
            if (!conn.identity.TryGetComponent(out NetPlayerVoice listener)) continue;

            if (listener.ServerTryPickChannel(this, speakerDead, radio, out VoiceChannel chaanel))
            {
                TargetReceiveVoice(conn, data, chaanel);
            }
        }
    }

    // [서버] 이 플레이어(듣는 쪽)가 speaker의 음성을 어떤 채널로 들을지 설정
    private bool ServerTryPickChannel(NetPlayerVoice speaker, bool speakerDead, bool radio, out VoiceChannel channel)
    {     
        channel = VoiceChannel.Proximity;

        // 관전자의 말은 관전자끼리만
        if (speakerDead)
        {
            channel = VoiceChannel.Spectator;
            return IsDead;
        }

        // 관전자는 생존자 음성을 전부 받고, 관전 카메라 위치 기준 3D 감쇠로 걸러 들음
        if (IsDead) return true;

        // 방울 무전은 방울을 가진 생존자에게
        if (radio && ServerCanHearRadio)
        {
            channel = VoiceChannel.Radio;
            return true;
        }

        float sqrDistance = (transform.position - speaker.transform.position).sqrMagnitude;
        return sqrDistance <= _proximityRange * _proximityRange;

    }
 
    // [받는 클라] 말한 플레이어 오브젝트에서 재생됨
    [TargetRpc(channel = Channels.Unreliable)]
    private void TargetReceiveVoice(NetworkConnectionToClient target, byte[] data, VoiceChannel channel)
    {
        _lastVoiceTime = Time.time;
        if (IsMutedLocally) return; // 압축 해제 전에 걸러서 CPU 절약

        PlayVoice(data, channel);
    }

    private void PlayVoice(byte[] data, VoiceChannel channel)
    {
        if (_decompressBuffer == null) return;

        EVoiceResult result = SteamUser.DecompressVoice(data, (uint)data.Length, _decompressBuffer, (uint)_decompressBuffer.Length, out uint written, _sampleRate);

        if (result != EVoiceResult.k_EVoiceResultOK || written == 0) return;

        if (channel == VoiceChannel.Radio)
        {
            _radioPlayback.Push(_decompressBuffer, (int)written);
            return;
        }

        // 관전자 음성은 몸이 없으니  2D로
        _proximityPlayback.SetSpatial(channel == VoiceChannel.Proximity);
        _proximityPlayback.Push(_decompressBuffer, (int)written);
    }
}
