using System.Collections.Generic;
using ProjectShaman.AI.Ghost;
using UnityEngine;

// Camera-owned presentation observation. Existing ReceiveStress decides local vs authority request.
[DisallowMultipleComponent, RequireComponent(typeof(PlayerStressController))]
public sealed class PlayerSightStress : MonoBehaviour
{
    [SerializeField] private Camera _playerCamera;
    [SerializeField, Min(.1f)] private float _sightDistance = 15f;
    [SerializeField, Range(10f, 160f)] private float _horizontalFov = 90f;
    [SerializeField, Min(.05f)] private float _requiredVisibleDuration = .6f;
    [SerializeField, Min(0f)] private float _retriggerCooldown = 8f;
    [SerializeField] private LayerMask _occlusionMask = ~0;
    [SerializeField, Min(.02f)] private float _sampleInterval = .1f;
    [SerializeField, Min(.1f)] private float _candidateRefreshInterval = 1f;
    [SerializeField, Min(0f)] private float _ghostStressAmount = 20f;

    private sealed class Observation
    {
        public Component Target;
        public Transform Point;
        public Renderer[] Renderers;
        public Collider Collider;
        public StressCause Cause;
        public float Amount, VisibleFor, HiddenSince;
        public bool Fired, WasVisible, Retained;
    }
    private readonly Dictionary<Component, Observation> _observations = new();
    private readonly List<Component> _removed = new();
    private readonly RaycastHit[] _hits = new RaycastHit[64];
    private PlayerStressController _stress;
    private PlayerInputReader _input;
    private float _nextSample, _nextRefresh, _lastSample;

    private void Awake()
    {
        _stress = GetComponent<PlayerStressController>();
        _input = GetComponent<PlayerInputReader>();
        if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>(true);
    }
    private void OnEnable() { _nextRefresh = _nextSample = 0f; _lastSample = Time.time; }
    private void LateUpdate()
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject) || !PlayerActionGuard.CanAct(gameObject) ||
            _playerCamera == null || !_playerCamera.isActiveAndEnabled ||
            (_input != null && (!_input.isActiveAndEnabled || _input.GameplayInputBlocked)))
        {
            foreach (var entry in _observations.Values) entry.VisibleFor = 0f;
            _lastSample = Time.time;
            return;
        }
        if (Time.time < _nextSample) return;
        float dt = Mathf.Clamp(Time.time - _lastSample, 0f, Mathf.Max(.02f, _sampleInterval) * 2f);
        _lastSample = Time.time; _nextSample = Time.time + Mathf.Max(.02f, _sampleInterval);
        if (Time.time >= _nextRefresh) { RefreshCandidates(); _nextRefresh = Time.time + Mathf.Max(.1f, _candidateRefreshInterval); }
        foreach (var entry in _observations.Values)
        {
            bool visible = IsVisible(entry);
            if (!visible)
            {
                entry.VisibleFor = 0f;
                if (entry.WasVisible) entry.HiddenSince = Time.time;
                if (entry.Fired && Time.time - entry.HiddenSince >= _retriggerCooldown) entry.Fired = false;
            }
            else if (!entry.Fired)
            {
                entry.VisibleFor += dt;
                if (entry.VisibleFor >= _requiredVisibleDuration)
                {
                    entry.Fired = true;
                    entry.VisibleFor = 0f;
                    _stress.ReceiveStress(entry.Amount, entry.Cause, entry.Target.gameObject);
                }
            }
            entry.WasVisible = visible;
        }
    }

    private void RefreshCandidates()
    {
        foreach (var entry in _observations.Values) entry.Retained = false;
        // Only manifested entities, never AI_Possession/ordinary villagers or server-only GhostId.
        foreach (var ghost in FindObjectsByType<AI_GhostEntity>(FindObjectsSortMode.None))
            Track(ghost, null, StressCause.GhostSight, _ghostStressAmount);
        foreach (var target in StressSightTarget.ActiveTargets)
            if (target != null)
            {
                // An explicit marker on a ghost overrides automatic discovery rather than double counting it.
                var ghost = target.GetComponentInParent<AI_GhostEntity>();
                Track(ghost != null ? (Component)ghost : target, target.SightPoint, target.Cause, target.StressAmount);
            }
        _removed.Clear();
        foreach (var entry in _observations) if (entry.Key == null || !entry.Value.Retained) _removed.Add(entry.Key);
        foreach (var key in _removed) _observations.Remove(key);
    }
    private void Track(Component target, Transform point, StressCause cause, float amount)
    {
        if (!_observations.TryGetValue(target, out var entry))
        {
            entry = new Observation { Target = target, HiddenSince = Time.time };
            _observations.Add(target, entry);
        }
        entry.Point = point; entry.Cause = cause; entry.Amount = amount; entry.Retained = true;
        entry.Renderers = target.GetComponentsInChildren<Renderer>(true);
        entry.Collider = target.GetComponentInChildren<Collider>();
    }
    private bool IsVisible(Observation entry)
    {
        if (entry.Target == null || !entry.Target.gameObject.activeInHierarchy) return false;
        var marker = entry.Target.GetComponentInChildren<StressSightTarget>();
        if (marker != null && !marker.SightActive) return false;
        bool hasVisibleGeometry = false;
        Bounds bounds = default;
        foreach (var renderer in entry.Renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                (_playerCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
            if (!hasVisibleGeometry) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
            hasVisibleGeometry = true;
        }
        if (!hasVisibleGeometry) return false;
        Vector3 point = entry.Point != null ? entry.Point.position : entry.Collider != null && entry.Collider.enabled ? entry.Collider.bounds.center : bounds.center;
        Vector3 viewport = _playerCamera.WorldToViewportPoint(point);
        if (viewport.z < _playerCamera.nearClipPlane || viewport.z > _playerCamera.farClipPlane ||
            viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
        Vector3 delta = point - _playerCamera.transform.position;
        float distance = delta.magnitude;
        if (distance > _sightDistance || distance < .001f) return false;
        Vector3 local = _playerCamera.transform.InverseTransformDirection(delta);
        if (Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) > _horizontalFov * .5f) return false;
        int count = Physics.RaycastNonAlloc(_playerCamera.transform.position, delta / distance, _hits, distance, _occlusionMask, QueryTriggerInteraction.Ignore);
        if (count == _hits.Length) return false; // Conservative if a very crowded ray overflows the buffer.
        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i].transform;
            if (hit.IsChildOf(transform) || hit.IsChildOf(entry.Target.transform)) continue;
            return false;
        }
        return true;
    }
}
