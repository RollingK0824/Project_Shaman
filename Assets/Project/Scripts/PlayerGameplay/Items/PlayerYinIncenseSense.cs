using System;
using Mirror;
using ProjectShaman.AI;
using UnityEngine;

// Server-owned possession becomes a direction/near flag, never a client host ID.
[DisallowMultipleComponent]
public sealed class PlayerYinIncenseSense : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _radius = 15f;
    [SerializeField, Min(0f)] private float _straightUpRadius = 1.5f;
    public event Action SenseRequested;
    private float _nextSample, _validUntil;
    private Vector3 _direction;
    private bool _near;

    public void Read(out Vector3 direction, out bool near)
    {
        if (Time.unscaledTime >= _nextSample)
        {
            _nextSample = Time.unscaledTime + .25f;
            if (!NetworkClient.active || NetworkServer.active)
            {
                TrySampleAuthoritative(out var sample, out var close);
                ApplyAuthoritativeSense(sample, close);
            }
            else if (PlayerLocalPresentation.IsLocal(gameObject)) SenseRequested?.Invoke();
        }
        bool valid = Time.unscaledTime <= _validUntil;
        direction = valid ? _direction : Vector3.zero;
        near = valid && _near;
    }

    public void ApplyAuthoritativeSense(Vector3 direction, bool near, float validForSeconds = .75f)
    {
        if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z)) return;
        direction.y = 0f;
        _direction = near ? Vector3.zero : Vector3.ClampMagnitude(direction, 1f);
        _near = near;
        _validUntil = Time.unscaledTime + Mathf.Clamp(validForSeconds, 0f, 2f);
    }

    // Caller validates sender + equipped incense; send this result to that client.
    // The same query is valid during day and night, with no Yin threshold gate.
    public bool TrySampleAuthoritative(out Vector3 direction, out bool near)
    {
        direction = Vector3.zero; near = false;
        if (NetworkClient.active && !NetworkServer.active) return false;
        float closest = _radius * _radius;
        bool found = false;
        foreach (var villager in FindObjectsByType<AI_Core>(FindObjectsSortMode.None))
        {
            var profile = villager.Profile;
            if (!villager.isActiveAndEnabled || profile == null || !profile.IsPossessed ||
                profile.ExorcisedDay >= 0 || villager.GetComponent<AI_Status>() is { IsDead: true } ||
                profile.Possession == null || !profile.Possession.isActiveAndEnabled) continue;
            Vector3 delta = villager.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > closest) continue;
            closest = delta.sqrMagnitude; direction = delta; found = true;
        }
        if (!found)
        {
            float strength = 0f;
            foreach (var source in YinEnergySource.ActiveSources)
            {
                if (source == null) continue;
                float influence = source.GetInfluence(transform.position);
                if (influence <= strength) continue;
                strength = influence; direction = source.transform.position - transform.position;
                direction.y = 0; found = true;
            }
        }
        near = found && direction.sqrMagnitude <= _straightUpRadius * _straightUpRadius;
        direction = near ? Vector3.zero : direction.normalized;
        return found;
    }
}

