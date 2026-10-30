using UnityEngine;
using UnityEngine.Rendering;

// Local presentation only. Samples the same spawn pose, speed and gravity as the projectile.
public sealed class BeanMugwortTrajectory : MonoBehaviour
{
    private LineRenderer _line;
    private readonly Vector3[] _points = new Vector3[96];
    private readonly RaycastHit[] _hits = new RaycastHit[64];
    public void Show(BeanMugwortUseAction action, in ItemUseContext context)
    {
        if (_line == null)
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = .012f;
            _line.numCornerVertices = 3;
            _line.numCapVertices = 3;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }
        _line.sharedMaterial = action.TrajectoryMaterial;
        _line.startColor = new Color(.9f, .8f, .55f, .9f);
        _line.endColor = new Color(.9f, .8f, .55f, .3f);
        action.GetSpawnPose(context, out var position, out _);
        Vector3 velocity = context.AimDirection * action.ThrowSpeed;
        float step = Time.fixedDeltaTime;
        int count = 1;
        _points[0] = position;
        int substeps = Mathf.Max(1, Mathf.CeilToInt(action.FlightLifetime / (step * (_points.Length - 1))));
        float elapsed = 0f;
        while (count < _points.Length && elapsed < action.FlightLifetime)
        {
            Vector3 next = position;
            for (int j = 0; j < substeps && elapsed < action.FlightLifetime; j++)
            {
                velocity += action.FlightGravity * step;
                next += velocity * step;
                elapsed += step;
            }
            Vector3 delta = next - position;
            float distance = delta.magnitude;
            int hits = Physics.SphereCastNonAlloc(position, action.CollisionRadius, delta.normalized, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            bool blocked = false;
            for (int i = 0; i < hits; i++)
            {
                if (!BeanMugwortProjectile.BlocksFlight(_hits[i].collider, context.Owner)) continue;
                if (_hits[i].distance <= nearest) { nearest = _hits[i].distance; blocked = true; }
            }
            position += delta.normalized * nearest;
            _points[count++] = position;
            if (blocked) break;
        }
        _line.positionCount = count;
        for (int i = 0; i < count; i++) _line.SetPosition(i, _points[i]);
        _line.enabled = true;
    }
    public void Hide() { if (_line != null) _line.enabled = false; }
}
