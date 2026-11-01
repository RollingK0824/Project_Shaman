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

    // World X/Z rectangle, and where its corners sit on the picture (0..1, from bottom-left; may fall outside).
    // Mapped as a perspective quad, since the picture is a tilted render.
    // Fitted to Gameplay.unity's houses, rice fields, well, shrine and stream against MapV3.png on 2026-11-01
    [SerializeField] private Vector2 _worldMin = new Vector2(-130f, -45f);
    [SerializeField] private Vector2 _worldMax = new Vector2(130f, 175f);
    [SerializeField] private Vector2 _uvMinXMinZ = new Vector2(-0.068f, -0.066f);
    [SerializeField] private Vector2 _uvMaxXMinZ = new Vector2(1.144f, -0.171f);
    [SerializeField] private Vector2 _uvMinXMaxZ = new Vector2(0.089f, 0.941f);
    [SerializeField] private Vector2 _uvMaxXMaxZ = new Vector2(0.946f, 0.955f);

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

        // Unit square to corner quad (Heckbert). A parallelogram quad reduces to plain bilinear
        Vector2 p0 = _uvMinXMinZ, p1 = _uvMaxXMinZ, p2 = _uvMaxXMaxZ, p3 = _uvMinXMaxZ;
        Vector2 d1 = p1 - p2, d2 = p3 - p2, d3 = p0 - p1 + p2 - p3;
        float g = 0f, h = 0f;
        float den = d1.x * d2.y - d2.x * d1.y;
        if (Mathf.Abs(den) > 1e-6f)
        {
            g = (d3.x * d2.y - d2.x * d3.y) / den;
            h = (d1.x * d3.y - d3.x * d1.y) / den;
        }

        Vector2 uv = (p1 * (1f + g) - p0) * u + (p3 * (1f + h) - p0) * v + p0;
        return uv / (g * u + h * v + 1f);
    }
}
