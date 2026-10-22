using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

// LAN 패널: 같은 네트워크의 방을 자동으로 찾아 목록에 표시
// 방을 고르면 주소 입력칸을 채움 → 참가는 기존 OnlineUI의 참가 버튼 그대로 사용
public class LanRoomListUI : MonoBehaviour
{
    [SerializeField] private RoomRowUI _rowPrefab;
    [SerializeField] private Transform _listContent;
    [SerializeField] private TMP_Text _emptyText;
    [SerializeField] private TMP_InputField _addressInput;      // OnlineUI의 주소 입력칸과 같은 것
    [SerializeField, Min(1f)] private float _roomTimeout = 7f;  // 이 시간 동안 응답이 없으면 닫힌 방으로 보고 제거

    private class Entry
    {
        public RoomInfo Room;
        public float LastSeen;
    }

    private readonly Dictionary<long, Entry> _rooms = new Dictionary<long, Entry>();
    private readonly List<RoomRowUI> _rows = new List<RoomRowUI>();
    private readonly List<long> _expired = new List<long>();

    private LanDiscovery _discovery;
    private string _selectedAddress;
    private bool _dirty;

    // RoomManager는 DontDestroyOnLoad라서 인스펙터 연결 대신 매번 찾음 (SteamLobbyUI와 같은 방식)
    private static LanDiscovery FindDiscovery() =>
        RoomManager.singleton != null ? RoomManager.singleton.GetComponent<LanDiscovery>() : null;

    private void OnEnable()
    {
        _rooms.Clear();
        _selectedAddress = null;
        _dirty = true;

        _discovery = FindDiscovery();
        if (_discovery == null)
        {
            Debug.LogWarning("[LanRoomListUI] RoomManager에 LanDiscovery가 없습니다.", this);
            return;
        }

        _discovery.RoomFound += HandleRoomFound;
        _discovery.TryStartDiscovery();
    }

    private void OnDisable()
    {
        if (_discovery == null) return;

        _discovery.RoomFound -= HandleRoomFound;

        // 호스트가 된 뒤(씬 전환으로 이 UI가 꺼질 때)에는 광고를 끄면 안 되므로 찾기 중일 때만 멈춤
        if (!NetworkServer.active) _discovery.StopDiscovery();

        _discovery = null;
    }

    private void HandleRoomFound(RoomInfo room, long serverId)
    {
        if (!_rooms.TryGetValue(serverId, out Entry entry))
        {
            entry = new Entry();
            _rooms.Add(serverId, entry);
        }

        entry.Room = room;
        entry.LastSeen = Time.unscaledTime;
        _dirty = true;
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        _expired.Clear();
        foreach (KeyValuePair<long, Entry> pair in _rooms)
        {
            if (now - pair.Value.LastSeen > _roomTimeout) _expired.Add(pair.Key);
        }

        foreach (long id in _expired)
        {
            _rooms.Remove(id);
            _dirty = true;
        }

        if (_dirty)
        {
            _dirty = false;
            RebuildList();
        }
    }

    private void RebuildList()
    {
        foreach (RoomRowUI row in _rows) Destroy(row.gameObject);
        _rows.Clear();

        RoomRowUI selectedRow = null;
        foreach (Entry entry in _rooms.Values)
        {
            RoomRowUI row = Instantiate(_rowPrefab, _listContent);
            row.Bind(entry.Room, OnRowClicked);
            _rows.Add(row);

            if (entry.Room.Address == _selectedAddress) selectedRow = row;
        }

        // 방이 하나뿐이고 아직 아무것도 안 골랐으면 자동 선택 → 바로 참가 가능
        if (selectedRow == null && _rows.Count == 1 && string.IsNullOrEmpty(_addressInput.text))
        {
            selectedRow = _rows[0];
        }

        if (selectedRow != null) Select(selectedRow);
        else _selectedAddress = null;

        if (_emptyText != null) _emptyText.gameObject.SetActive(_rows.Count == 0);
    }

    private void OnRowClicked(RoomRowUI clicked) => Select(clicked);

    private void Select(RoomRowUI selected)
    {
        foreach (RoomRowUI row in _rows) row.SetSelected(row == selected);

        _selectedAddress = selected.Room.Address;
        _addressInput.text = _selectedAddress;
    }
}