using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.World;

// Protects the registered home area through the existing AI target-filter contract.
// Physical path exclusion and remote presentation belong to the AI/network adapters.
public sealed class HouseBarrierWard : MonoBehaviour, IGhostBarrier
{
    private static readonly Dictionary<PlaceArea, HouseBarrierWard> Active = new();
    private PlaceArea _home;
    private string _residentId;
    private double _expiresAt, _lastClockTime;
    private TimeManager _clock;
    private bool _armed;
    private Action<HouseBarrierWard> _despawn;
    private Material _paperMaterial;
    private Renderer _protectionGlow;
    public PlaceArea Home => _home;
    public string HomeId => _home != null ? _home.PlaceId : null;
    public string ResidentId => _residentId;
    public float RemainingDuration => IsActive ? (float)Math.Max(0, _expiresAt - _clock.Elapsed) : 0f;
    public bool IsActive => _armed && isActiveAndEnabled && _home != null && _home.isActiveAndEnabled &&
        _home.OwnerId == _residentId && _clock != null && TimeManager.Existing == _clock &&
        _clock.IsRunning && double.IsFinite(_clock.Elapsed) && _clock.Elapsed >= _lastClockTime && _clock.Elapsed < _expiresAt &&
        (!NetworkClient.active || NetworkServer.active);

    internal static bool HasRunningClock => TimeManager.Existing != null && TimeManager.Existing.IsRunning &&
        double.IsFinite(TimeManager.Existing.Elapsed) && TimeManager.Existing.Elapsed >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();

    public static bool IsProtecting(PlaceArea home) => home != null &&
        Active.TryGetValue(home, out var ward) && ward != null && ward.IsActive;

    internal void Prepare(PlaceArea home, Vector3 point, Vector3 normal, Texture paper,
        Action<HouseBarrierWard> despawn)
    {
        _home = home;
        _residentId = home.OwnerId;
        _despawn = despawn;
        transform.SetPositionAndRotation(point + normal * .012f, Quaternion.LookRotation(normal));
        transform.SetParent(home.transform, true);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visual.name = "Attached talisman";
        visual.transform.SetParent(transform, false);
        // Unity's Quad faces local -Z; face its front out along the wall normal.
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        float aspect = paper != null && paper.height > 0 ? (float)paper.width / paper.height : .5f;
        visual.transform.localScale = new Vector3(.28f * aspect, .28f, 1f);
        visual.GetComponent<Collider>().enabled = false;
        // Include the cutout material in builds, so the alpha-test shader variant is retained.
        var template = Resources.Load<Material>("PlayerItems/HouseBarrierPaper");
        if (template == null) throw new InvalidOperationException("House ward paper material is missing.");
        _paperMaterial = new Material(template) { name = "House ward paper (runtime)" };
        if (paper != null) _paperMaterial.SetTexture("_BaseMap", paper);
        visual.GetComponent<Renderer>().sharedMaterial = _paperMaterial;
        var glowMaterial = Resources.Load<Material>("PlayerItems/HouseBarrierGlow");
        if (glowMaterial != null)
        {
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "Active house protection";
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, .002f);
            glow.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glow.transform.localScale = new Vector3(.38f * aspect, .38f, 1f);
            glow.GetComponent<Collider>().enabled = false;
            _protectionGlow = glow.GetComponent<Renderer>();
            _protectionGlow.sharedMaterial = glowMaterial;
            var glowProperties = new MaterialPropertyBlock();
            if (paper != null) glowProperties.SetTexture("_BaseMap", paper);
            _protectionGlow.SetPropertyBlock(glowProperties);
            _protectionGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Remote Prepare is invoked only for a confirmed SyncList entry. It must
            // show protection without registering an authoritative AI barrier.
            _protectionGlow.enabled = NetworkClient.active && !NetworkServer.active;
        }
    }

    internal void Arm()
    {
        _clock = TimeManager.Existing;
        if (!HasRunningClock) throw new InvalidOperationException("House protection requires a running game clock.");
        bool firstActivation = !_armed;
        _lastClockTime = _clock.Elapsed;
        _expiresAt = (Math.Floor(_lastClockTime / TimeManager.CYCLE_DURATION) + 1) * TimeManager.CYCLE_DURATION;
        _armed = true;
        if (_protectionGlow != null) _protectionGlow.enabled = true;
        Active[_home] = this;
        GhostBarrierRegistry.Register(this);
        if (firstActivation)
        {
            PlayerSoundBank.PlayAt(PlayerSoundCue.Paper, transform.position);
            ItemVfxPresentation.Barrier(transform.position);
        }
    }

    public bool Blocks(Vector3 position, GhostActionVerb verb) => IsActive && _home.Contains(position);
    private void Update()
    {
        if (!_armed) return;
        if (!IsActive) { Expire(); return; }
        _lastClockTime = _clock.Elapsed;
    }
    public void Expire()
    {
        Unregister();
        gameObject.SetActive(false);
        var despawn = _despawn;
        _despawn = null;
        if (despawn != null) despawn(this); else Destroy(gameObject);
    }
    private void Unregister()
    {
        _armed = false;
        if (_protectionGlow != null) _protectionGlow.enabled = false;
        GhostBarrierRegistry.Unregister(this);
        if (!ReferenceEquals(_home, null) && Active.TryGetValue(_home, out var current) && current == this)
            Active.Remove(_home);
    }
    private void OnDisable() => Unregister();
    private void OnDestroy()
    {
        Unregister();
        if (_paperMaterial != null) Destroy(_paperMaterial);
    }
}
