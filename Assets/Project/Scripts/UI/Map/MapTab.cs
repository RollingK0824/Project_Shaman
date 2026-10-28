using System.Collections.Generic;
using Mirror;
using ProjectShaman.Map;
using TMPro;
using UnityEngine;

// Notebook Map tab. Positions are read once when the tab is shown, not live.
public class MapTab : MonoBehaviour
{
    // The Image showing the map picture. Its rect must have the picture's aspect (no Preserve Aspect letterboxing)
    [SerializeField]
    private RectTransform _mapRect;

    [SerializeField]
    private MapMarkerUI _markerPrefab;

    // Full-stretch children of _mapRect, in this order so player markers draw above house pins
    [SerializeField]
    private RectTransform _housePinRoot;

    [SerializeField]
    private RectTransform _markerRoot;

    [SerializeField]
    private RectTransform _tooltip;     // last child of _mapRect, Raycast Target off

    [SerializeField]
    private TMP_Text _tooltipText;

    [SerializeField]
    private float _tooltipOffset = 12f;     // px above the pin

    [SerializeField]
    private TMP_Text _locationText;     // optional, "현재 위치: ..."

    // World X/Z rectangle, and where its corners sit on the picture (0..1, from bottom-left).
    // Measured from Gameplay.unity's houses against MapV2.png on 2026-10-28
    [SerializeField] private Vector2 _worldMin = new Vector2(-180f, -60f);
    [SerializeField] private Vector2 _worldMax = new Vector2(170f, 210f);
    [SerializeField] private Vector2 _uvMinXMinZ = new Vector2(0.042f, 0.067f);
    [SerializeField] private Vector2 _uvMaxXMinZ = new Vector2(0.962f, 0.067f);
    [SerializeField] private Vector2 _uvMinXMaxZ = new Vector2(0.042f, 0.976f);
    [SerializeField] private Vector2 _uvMaxXMaxZ = new Vector2(0.962f, 0.976f);

    [SerializeField]
    private Color _deadColor = Color.gray;

    private readonly List<MapMarkerUI> _markers = new List<MapMarkerUI>();
    private int _requestId;

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        _requestId++;   // drop a network answer that arrives after closing
        HideTooltip();
    }

    public void ShowTooltip(string text, RectTransform pin)
    {
        if (_tooltip == null || string.IsNullOrEmpty(text)) return;

        _tooltipText.text = text;
        _tooltip.position = pin.position;
        _tooltip.anchoredPosition += Vector2.up * _tooltipOffset;
        _tooltip.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        foreach (MapMarkerUI marker in _markers) Destroy(marker.gameObject);
        _markers.Clear();

        if (_locationText != null) _locationText.text = "";

        HideTooltip();
        if (_housePinRoot != null)
        {
            // true: hidden pins come back when their house gets an owner
            foreach (MapHousePinUI pin in _housePinRoot.GetComponentsInChildren<MapHousePinUI>(true)) pin.Refresh(this);
        }

        NetworkIdentity local = NetworkClient.localPlayer;
        if (local != null)
        {
            local.TryGetComponent(out NetPlayerStatus status);
            MapPlayerSnapshot me = new MapPlayerSnapshot
            {
                NetId = local.netId,
                ColorId = status != null ? status.ColorId : -1,
                Name = "나",
                Position = local.transform.position,
                Forward = local.transform.forward,
                IsDead = status != null && status.IsDead,
            };
            SpawnMarker(me, true);

            if (_locationText != null && MapAreaRegistry.TryResolve(me.Position, out IMapArea area))
                _locationText.text = $"현재 위치: {area.DisplayName}";
        }

        if (MapPlayerSource.Current == null) return;

        int id = ++_requestId;
        MapPlayerSource.Current.RequestOtherPlayers(players =>
        {
            if (id != _requestId || !isActiveAndEnabled) return;
            foreach (MapPlayerSnapshot player in players) SpawnMarker(player, false);
        });
    }

    private void SpawnMarker(MapPlayerSnapshot player, bool isLocal)
    {
        Vector2 uv = WorldToUv(player.Position);

        float angle = float.NaN;
        Vector3 forward = new Vector3(player.Forward.x, 0f, player.Forward.z);
        if (forward.sqrMagnitude > 0.0001f)
        {
            // Map a point a bit ahead too, so the arrow follows the picture's tilt
            Vector2 dir = (WorldToUv(player.Position + forward.normalized) - uv) * _mapRect.rect.size;
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        }

        Color color = player.IsDead ? _deadColor : PlayerColors.Get(player.ColorId);

        MapMarkerUI marker = Instantiate(_markerPrefab, _markerRoot != null ? _markerRoot : _mapRect);
        marker.Set(uv, angle, color, player.Name, isLocal);
        if (!isLocal) marker.transform.SetAsFirstSibling();   // the local marker stays on top
        _markers.Add(marker);
    }

    private Vector2 WorldToUv(Vector3 position)
    {
        float u = Mathf.InverseLerp(_worldMin.x, _worldMax.x, position.x);
        float v = Mathf.InverseLerp(_worldMin.y, _worldMax.y, position.z);
        Vector2 bottom = Vector2.LerpUnclamped(_uvMinXMinZ, _uvMaxXMinZ, u);
        Vector2 top = Vector2.LerpUnclamped(_uvMinXMaxZ, _uvMaxXMaxZ, u);
        return Vector2.LerpUnclamped(bottom, top, v);
    }
}
