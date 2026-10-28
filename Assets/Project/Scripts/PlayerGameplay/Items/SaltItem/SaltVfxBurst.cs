using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

// Disposable world-space presentation. No damage, triggers, raycast targets or item ownership.
public sealed class SaltVfxBurst : MonoBehaviour
{
    private static readonly List<SaltVfxBurst> Live = new();
    private static readonly RaycastHit[] Hits = new RaycastHit[32];
    public bool IsImpact { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList() => Live.Clear();
    private void OnDestroy() => Live.Remove(this);

    private static SaltVfxBurst Create(Vector3 position, Vector3 direction, bool impact)
    {
        var settings = SaltVfxSettings.Default;
        if (settings == null || settings.GrainMaterial == null || settings.DustMaterial == null ||
            (NetworkServer.active && !NetworkClient.active)) return null;
        Live.RemoveAll(v => v == null);
        if (Live.Count >= Mathf.Max(4, settings.MaxLiveBursts))
        {
            var oldest = Live[0]; Live.RemoveAt(0);
            oldest.gameObject.SetActive(false); Destroy(oldest.gameObject);
        }
        var go = new GameObject(impact ? "Salt Confirmed Impact VFX" : "Salt Spray VFX");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.up));
        var burst = go.AddComponent<SaltVfxBurst>(); burst.IsImpact = impact; Live.Add(burst);
        Destroy(go, 1.4f);
        return burst;
    }

    public static bool Spray(GameObject owner, Vector3 emitter, Ray aim, float reach, float halfWidth)
    {
        if (owner == null || !PlayerActionGuard.CanAct(owner) || aim.direction.sqrMagnitude < .001f) return false;
        aim.direction = aim.direction.normalized;
        // Keep the visible burst in front of nearby walls, even with a camera-mounted held item.
        Vector3 toEmitter = emitter - aim.origin;
        float clear = ClearDistance(owner.transform, new Ray(aim.origin, toEmitter.normalized), toEmitter.magnitude);
        if (clear < toEmitter.magnitude) emitter = aim.origin + toEmitter.normalized * Mathf.Max(0f, clear - .03f);
        // A third-person camera can be farther behind the hand than the whole spray range.
        // Project the aim beyond the emitter, then cap actual travel from the hand below.
        float cameraToHand = Mathf.Max(0f, Vector3.Dot(emitter - aim.origin, aim.direction));
        float aimReach = ClearDistance(owner.transform, aim, cameraToHand + Mathf.Max(.03f, reach));
        Vector3 target = aim.GetPoint(aimReach);
        Vector3 direction = target - emitter;
        if (direction.sqrMagnitude < .0001f || Vector3.Dot(direction, aim.direction) <= 0f) direction = aim.direction * .04f;
        float travel = ClearDistance(owner.transform, new Ray(emitter, direction.normalized), Mathf.Min(reach, direction.magnitude));
        if (travel < .025f) return false;
        var burst = Create(emitter, direction.normalized, false);
        if (burst == null) return false;
        var s = SaltVfxSettings.Default;
        var grains = burst.Particles("Coarse salt", s.GrainMaterial, s.GrainSize, 0f, 1f, false);
        // Explicit emission gives every grain a lifetime bounded by its travel speed.
        for (int i = 0; i < Mathf.Clamp(s.SprayGrains, 8, 160); i++)
        {
            float speed = Random.Range(Mathf.Max(.1f, s.SpraySpeed.x), Mathf.Max(.1f, s.SpraySpeed.y));
            Vector2 spread = Random.insideUnitCircle * Mathf.Min(halfWidth, travel * .11f);
            Vector3 velocity = burst.transform.TransformDirection(new Vector3(spread.x, spread.y + .025f, travel).normalized) * speed;
            var p = new ParticleSystem.EmitParams
            {
                position = emitter + burst.transform.TransformVector((Vector3)Random.insideUnitCircle * .012f),
                velocity = velocity,
                startLifetime = Mathf.Min(travel / speed, .8f),
                startSize = Random.Range(s.GrainSize.x, s.GrainSize.y),
                startColor = Color.Lerp(new Color(.77f, .78f, .75f), new Color(1f, .985f, .94f), Random.value),
                rotation = Random.Range(0f, 360f)
            };
            grains.Emit(p, 1);
        }
        var gravity = grains.main; gravity.gravityModifier = .22f;
        var dust = burst.Particles("Fine salt powder", s.DustMaterial, new Vector2(.035f, .075f), .1f, s.PowderOpacity * .65f, true);
        burst.EmitFan(dust, Mathf.Clamp(s.SprayPowder, 0, 60), 1.8f, 3f, Mathf.Min(.3f, travel / 3f), 9f, .015f);
        return true;
    }

    public static void Impact(Vector3 point, Vector3 normal)
    {
        normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
        var burst = Create(point + normal * .018f, normal, true);
        if (burst == null) return;
        var s = SaltVfxSettings.Default;
        var grains = burst.Particles("Contact salt grains", s.GrainMaterial, new Vector2(.003f, .008f), .55f, 1f, false);
        burst.EmitFan(grains, Mathf.Clamp(s.ImpactGrains, 4, 80), .6f, 1.7f, .48f, 74f, .035f);
        var dust = burst.Particles("Contact powder puff", s.DustMaterial, new Vector2(.08f, .16f), .04f, s.PowderOpacity, true);
        burst.EmitFan(dust, Mathf.Clamp(s.ImpactPowder, 0, 32), .16f, .5f, .62f, 72f, .055f);
    }

    private ParticleSystem Particles(string label, Material material, Vector2 size, float gravity, float alpha, bool powder)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        var ps = child.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = .1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 180; main.startSpeed = 0; main.startLifetime = .65f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
        main.startColor = new Color(.94f, .93f, .88f, alpha); main.gravityModifier = gravity;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(powder ? 0f : 1f, 0), new GradientAlphaKey(1, powder ? .12f : .05f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var scale = ps.sizeOverLifetime; scale.enabled = powder;
        scale.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .5f, 1, 2.1f));
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.minParticleSize = 0; renderer.maxParticleSize = .08f;
        ps.Play();
        return ps;
    }
    private void EmitFan(ParticleSystem ps, int count, float minSpeed, float maxSpeed, float lifetime, float angle, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            float cos = Mathf.Lerp(Mathf.Cos(angle * Mathf.Deg2Rad), 1f, Random.value);
            float sin = Mathf.Sqrt(1f - cos * cos), azimuth = Random.value * Mathf.PI * 2;
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = transform.position + transform.TransformVector((Vector3)Random.insideUnitCircle * radius),
                velocity = transform.TransformDirection(new Vector3(sin * Mathf.Cos(azimuth), sin * Mathf.Sin(azimuth), cos)) * Random.Range(minSpeed, maxSpeed),
                startLifetime = Random.Range(lifetime * .65f, lifetime)
            }, 1);
        }
    }
    private static float ClearDistance(Transform owner, Ray ray, float distance)
    {
        if (distance <= .001f) return distance;
        int count = Physics.RaycastNonAlloc(ray, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        // Full buffers cannot guarantee the nearest result. Allocate only on this exceptional case.
        var hits = count == Hits.Length ? Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore) : Hits;
        if (hits != Hits) count = hits.Length;
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(owner)) distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - .015f));
        return distance;
    }
}
