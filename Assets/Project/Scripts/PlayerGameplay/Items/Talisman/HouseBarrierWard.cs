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
    private float _expiresAt;
    private bool _armed;
    private Action<HouseBarrierWard> _despawn;
    private Material _paperMaterial;
    public PlaceArea Home => _home;
    public string HomeId => _home != null ? _home.PlaceId : null;
    public string ResidentId => _residentId;
    public float RemainingDuration => IsActive ? Mathf.Max(0f, _expiresAt - Time.time) : 0f;
    public bool IsActive => _armed && isActiveAndEnabled && _home != null && _home.isActiveAndEnabled &&
        _home.OwnerId == _residentId && Time.time < _expiresAt &&
        (!NetworkClient.active || NetworkServer.active);

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
        visual.transform.localScale = new Vector3(.14f, .28f, 1f);
        visual.GetComponent<Collider>().enabled = false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("House ward requires the URP Unlit shader.");
        _paperMaterial = new Material(shader) { name = "House ward paper (runtime)" };
        _paperMaterial.SetColor("_BaseColor", new Color(.84f, .76f, .58f));
        _paperMaterial.SetFloat("_Cull", 0f);
        if (paper != null) _paperMaterial.SetTexture("_BaseMap", paper);
        visual.GetComponent<Renderer>().sharedMaterial = _paperMaterial;
    }

    internal void Arm(float duration)
    {
        bool firstActivation = !_armed;
        _expiresAt = Time.time + duration;
        _armed = true;
        Active[_home] = this;
        GhostBarrierRegistry.Register(this);
        if (firstActivation) PlayerSoundBank.PlayAt(PlayerSoundCue.Paper, transform.position);
    }

    public bool Blocks(Vector3 position, GhostActionVerb verb) => IsActive && _home.Contains(position);
    private void Update() { if (_armed && !IsActive) Expire(); }
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
