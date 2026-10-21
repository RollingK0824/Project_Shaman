using UnityEngine;

// Presentation only. No colliders, authority, consumption or gameplay event writes.
[DisallowMultipleComponent]
public sealed class BarrierWardVisual : MonoBehaviour
{
    [SerializeField] private MeshRenderer _shell;
    [SerializeField] private LineRenderer _seal;
    [SerializeField] private LineRenderer _charges;
    [SerializeField, Range(0f, 1f)] private float _opacity = .7f;
    [SerializeField, Min(.01f)] private float _sealRadius = .23f;
    [SerializeField, Min(.05f)] private float _flashDuration = .45f;
    [SerializeField, Min(.05f)] private float _fadeDuration = .6f;
    private static readonly int Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int Pulse = Shader.PropertyToID("_Pulse");
    private MaterialPropertyBlock _properties;
    private BarrierWard _ward;
    private bool _shown, _fading;
    private int _remaining;
    private float _flashAt = -10f, _fadeAt;
    public bool IsShowing => _shown;
    public bool IsFading => _fading;

    private void Awake() => _properties = new MaterialPropertyBlock();
    private void OnEnable()
    {
        SetVisible(false);
        _ward = GetComponentInParent<BarrierWard>();
        if (_ward == null) return;
        _ward.Changed += HandleChanged;
        _ward.Expired += HandleExpired;
        if (_ward.IsActive) HandleChanged(_ward);
    }
    private void HandleChanged(BarrierWard ward)
    {
        ApplyReplicatedState(ward.GetComponent<SphereCollider>().radius,
            ward.RemainingCharges, ward.MaxCharges, ward.IsActive);
    }
    private void HandleExpired(BarrierWard ward) => Dissolve();

    // Optional future network adapter entry point. Updates visuals only, never arms a ward.
    public void ApplyReplicatedState(float radius, int remainingCharges, int maxCharges, bool active)
    {
        if (_fading) return;
        if (!active) { if (_shown) Dissolve(); return; }
        if (!float.IsFinite(radius) || radius <= 0f || maxCharges <= 0 || _shell == null) return;
        int remaining = Mathf.Clamp(remainingCharges, 0, maxCharges);
        if (!_shown || remaining < _remaining) _flashAt = Time.unscaledTime;
        _remaining = remaining;
        _shell.transform.localScale = Vector3.one * (radius * 2f);
        Arc(_seal, _sealRadius, 360f, true);
        Arc(_charges, _sealRadius * .82f, 280f * remaining / maxCharges, false);
        _shown = true;
        SetVisible(true);
        if (_charges != null) _charges.enabled = remaining > 0;
        Draw();
    }
    private static void Arc(LineRenderer line, float radius, float degrees, bool loop)
    {
        if (line == null) return;
        const int segments = 80;
        line.useWorldSpace = false; line.loop = loop;
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (40f + degrees * i / segments) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
    }
    private void LateUpdate()
    {
        if (!_shown) return;
        if (_fading && Time.unscaledTime - _fadeAt >= _fadeDuration) { Destroy(gameObject); return; }
        Draw();
    }
    private void Draw()
    {
        if (_properties == null) _properties = new MaterialPropertyBlock();
        float flash = 1f - Mathf.Clamp01((Time.unscaledTime - _flashAt) / Mathf.Max(.05f, _flashDuration));
        float fade = _fading ? 1f - Mathf.Clamp01((Time.unscaledTime - _fadeAt) / Mathf.Max(.05f, _fadeDuration)) : 1f;
        float breath = .88f + .12f * Mathf.Sin(Time.unscaledTime * 1.6f);
        _properties.SetFloat(Opacity, _opacity * fade * breath);
        _properties.SetFloat(Pulse, flash * flash);
        if (_shell != null) _shell.SetPropertyBlock(_properties);
        _properties.SetFloat(Opacity, fade * (.65f + .35f * flash));
        if (_seal != null) _seal.SetPropertyBlock(_properties);
        if (_charges != null) _charges.SetPropertyBlock(_properties);
    }
    private void Dissolve()
    {
        if (_fading || !_shown) return; // A failed placement must not produce a success effect.
        _fading = true; _fadeAt = Time.unscaledTime;
        Unbind();
        // The authoritative ward disappears immediately. Only this collider-free visual lingers.
        transform.SetParent(null, true);
    }
    private void SetVisible(bool visible)
    {
        if (_shell != null) _shell.enabled = visible;
        if (_seal != null) _seal.enabled = visible;
        if (_charges != null) _charges.enabled = visible;
    }
    private void Unbind()
    {
        if (_ward != null) { _ward.Changed -= HandleChanged; _ward.Expired -= HandleExpired; }
        _ward = null;
    }
    private void OnDisable() { Unbind(); SetVisible(false); }
}
