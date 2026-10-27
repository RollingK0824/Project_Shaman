using System.Collections.Generic;
using Mirror;
using ProjectShaman.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD compass strip at the top of the screen. North = world +Z, East = +X (same as the Map tab).
public class CompassBar : MonoBehaviour
{
    // Zero-width RectTransform, anchors + pivot centre, inside a RectMask2D viewport. Labels are spawned under it
    [SerializeField]
    private RectTransform _strip;

    // Inactive templates inside _strip, anchors + pivot centre. _tickTemplate is optional
    [SerializeField]
    private TMP_Text _labelTemplate;

    [SerializeField]
    private Image _tickTemplate;

    [SerializeField]
    private float _pixelsPerDegree = 4f;

    [SerializeField]
    private float _tickStep = 15f;

    // One label every 360 / length degrees, starting at north
    [SerializeField]
    private string[] _labels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    // Quest marks sit in a row just under the bar, outside the strip's mask (it would clip them)
    [SerializeField]
    private float _questIconOffsetY = -30f;

    [SerializeField]
    private float _questIconFontSize = 26f;

    private readonly List<TMP_Text> _questIcons = new List<TMP_Text>();
    private NetworkIdentity _cachedPlayer;
    private PlayerSpectatorController _spectator;
    private Camera _playerCamera;

    private void Awake()
    {
        BuildStrip();
    }

    private void LateUpdate()
    {
        Transform view = GetView();
        if (view == null || _strip == null)
        {
            return;
        }

        Vector3 forward = view.forward;
        if (forward.x * forward.x + forward.z * forward.z < 0.0001f)
        {
            return;     // looking straight up/down, keep the last heading
        }

        float heading = GetHeading(forward);
        _strip.anchoredPosition = new Vector2(-heading * _pixelsPerDegree, _strip.anchoredPosition.y);
        PlaceQuestIcons(view.position, heading);
    }

    // Quest marks under the bar: "!" an offer can be taken there, "?" a quest can be reported there.
    // One beyond the bar's width waits at that edge, so the player knows which way to turn
    private void PlaceQuestIcons(Vector3 from, float heading)
    {
        if (_labelTemplate == null)
        {
            return;
        }

        IReadOnlyList<AI_QuestMarker> markers = AI_QuestMarker.Shown;
        RectTransform bar = (RectTransform)transform;
        float halfWidth = bar.rect.width * 0.5f;
        int used = 0;

        foreach (AI_QuestMarker marker in markers)
        {
            Vector3 offset = marker.transform.position - from;
            if (offset.x * offset.x + offset.z * offset.z < 0.01f)
            {
                continue;
            }

            if (used == _questIcons.Count)
            {
                TMP_Text created = Instantiate(_labelTemplate, bar);
                created.fontStyle = FontStyles.Bold;
                created.fontSize = _questIconFontSize;
                _questIcons.Add(created);
            }

            TMP_Text icon = _questIcons[used++];
            icon.text = marker.Symbol;
            icon.color = marker.Color;
            float x = Mathf.Clamp(Mathf.DeltaAngle(heading, GetHeading(offset)) * _pixelsPerDegree, -halfWidth, halfWidth);
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = new Vector2(x, _questIconOffsetY);
        }

        for (int i = used; i < _questIcons.Count; i++)
        {
            _questIcons[i].gameObject.SetActive(false);
        }
    }

    public static float GetHeading(Vector3 forward)
    {
        return Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
    }

    // Spectator camera while spectating (it isn't tagged MainCamera), else the local player's camera
    private Transform GetView()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local == null)
        {
            return Camera.main != null ? Camera.main.transform : null;
        }

        if (local != _cachedPlayer)
        {
            _cachedPlayer = local;
            _spectator = local.GetComponentInChildren<PlayerSpectatorController>(true);
            _playerCamera = local.GetComponentInChildren<Camera>(true);
        }

        if (_spectator != null && _spectator.IsSpectating && _spectator.SpectatorCamera != null)
        {
            return _spectator.SpectatorCamera.transform;
        }

        return _playerCamera != null ? _playerCamera.transform : local.transform;
    }

    // Lays out -180°..540° so any heading in 0..360 has a full screen of labels on both sides (no wrap logic)
    private void BuildStrip()
    {
        if (_strip == null || _labelTemplate == null || _labels.Length == 0)
        {
            return;
        }

        float labelStep = 360f / _labels.Length;
        for (float deg = -180f; deg <= 540f; deg += labelStep)
        {
            int index = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / labelStep) % _labels.Length;
            TMP_Text label = Instantiate(_labelTemplate, _strip);
            label.text = _labels[index];
            Place(label.rectTransform, deg);
        }

        if (_tickTemplate != null && _tickStep > 0f)
        {
            for (float deg = -180f; deg <= 540f; deg += _tickStep)
            {
                if (Mathf.Abs(Mathf.Repeat(deg, labelStep)) < 0.01f)
                {
                    continue;   // a label already sits here
                }

                Image tick = Instantiate(_tickTemplate, _strip);
                Place(tick.rectTransform, deg);
            }
        }
    }

    private void Place(RectTransform item, float deg)
    {
        item.gameObject.SetActive(true);
        item.anchoredPosition = new Vector2(deg * _pixelsPerDegree, item.anchoredPosition.y);
    }
}
