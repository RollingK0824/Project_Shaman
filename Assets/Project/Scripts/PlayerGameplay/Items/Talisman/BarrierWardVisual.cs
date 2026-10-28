using UnityEngine;

// Presentation only. No colliders, authority, consumption or gameplay event writes.
[DisallowMultipleComponent]
public sealed class BarrierWardVisual : MonoBehaviour
{
    [SerializeField] private MeshRenderer _shell;
    [SerializeField, Range(0f, 1f)] private float _opacity = .7f;
    [SerializeField, Min(.05f)] private float _flashDuration = .45f;
    [SerializeField, Min(.05f)] private float _fadeDuration = .6f;
    private static readonly int Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int Pulse = Shader.PropertyToID("_Pulse");
    private MaterialPropertyBlock _properties;
    private BarrierWard _ward;
    private bool _shown, _fading;
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
    private void HandleChanged(BarrierWard ward) => ApplyReplicatedState(ward.Radius, ward.IsActive);
    private void HandleExpired(BarrierWard ward) => Dissolve();

    // Network presentation only. Follow the replicated owner externally; never grants protection.
    public void ApplyReplicatedState(float radius, bool active)
    {
        if (_fading) return;
        if (!active) { if (_shown) Dissolve(); return; }
        if (!float.IsFinite(radius) || radius <= 0f || _shell == null) return;
        if (!_shown)
        {
            _flashAt = Time.unscaledTime;
            PlayerSoundBank.PlayAt(PlayerSoundCue.Barrier, transform.position);
        }
        _shell.transform.localScale = Vector3.one * (radius * 2f);
        _shown = true;
        SetVisible(true);
        Draw();
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
    }
    private void Dissolve()
    {
        if (_fading || !_shown) return; // A failed activation must not produce a success effect.
        _fading = true; _fadeAt = Time.unscaledTime;
        Unbind();
        // The authoritative ward disappears immediately. Only this collider-free visual lingers.
        transform.SetParent(null, true);
    }
    private void SetVisible(bool visible)
    {
        if (_shell != null) _shell.enabled = visible;
    }
    private void Unbind()
    {
        if (_ward != null) { _ward.Changed -= HandleChanged; _ward.Expired -= HandleExpired; }
        _ward = null;
    }
    private void OnDisable() { Unbind(); SetVisible(false); }
}
