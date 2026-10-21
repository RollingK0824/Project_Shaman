using UnityEngine;

namespace OrnateBellRattle
{
    // Bounded secondary motion. No rigid bodies or external packages required.
    [DefaultExecutionOrder(1000)]
    public sealed class BellSwing : MonoBehaviour
    {
        public Vector3 restCenter = new Vector3(0, -0.03f, 0);
        [Range(1, 30)] public float maxAngle = 12;
        [Range(1, 8)] public float frequency = 3.1f;
        [Range(0.05f, 2)] public float damping = 0.22f;
        [Range(0, 2)] public float response = 0.65f;
        public AudioClip[] chimes;
        [Range(0, 1)] public float volume = 0.18f;
        public float soundSpeedThreshold = 0.8f;
        public float minimumSoundInterval = 0.13f;
        public float pitch = 1;
        Quaternion restRotation, previousBase;
        Vector3 previousPosition, previousVelocity, filteredAcceleration;
        Vector3 angle, angularVelocity;
        bool initialized;
        float cooldown;
        int samples, clipIndex;
        AudioSource speaker;

        void Awake()
        {
            restRotation = transform.localRotation;
            speaker = GetComponent<AudioSource>();
            if (!speaker) speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1;
            speaker.dopplerLevel = 0;
            speaker.minDistance = 0.4f;
            speaker.maxDistance = 8;
        }
        void OnEnable() { initialized = false; }
        void OnDisable() { transform.localRotation = restRotation; }

        public void ResetMotion()
        {
            initialized = false;
            angle = angularVelocity = filteredAcceleration = Vector3.zero;
            transform.localRotation = restRotation;
        }

        void LateUpdate() { Step(Time.deltaTime); }

        public void Step(float dt)
        {
            if (dt <= 0) return;
            Quaternion basis = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restRotation;
            Vector3 position = transform.position;
            if (!initialized || dt > 0.12f || (position - previousPosition).sqrMagnitude > 0.25f)
            {
                previousPosition = position; previousVelocity = Vector3.zero;
                previousBase = basis; angle = angularVelocity = filteredAcceleration = Vector3.zero;
                initialized = true; samples = 0; cooldown = 0.2f;
                transform.localRotation = restRotation; return;
            }
            dt = Mathf.Max(dt, 0.0001f);
            Vector3 velocity = (position - previousPosition) / dt;
            Vector3 acceleration = samples++ > 0 ? (velocity - previousVelocity) / dt : Vector3.zero;
            acceleration = Vector3.ClampMagnitude(acceleration, 50);
            filteredAcceleration = Vector3.Lerp(filteredAcceleration, acceleration, 1 - Mathf.Exp(-25 * dt));
            Quaternion delta = Quaternion.Inverse(basis) * previousBase;
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180) degrees -= 360;
            if (Mathf.Abs(degrees) > 0.0001f && axis.sqrMagnitude > 0.1f)
                angle += axis.normalized * (degrees * Mathf.Deg2Rad * response);
            Vector3 localAcceleration = Quaternion.Inverse(basis) * filteredAcceleration;
            float length = Mathf.Max(0.015f, restCenter.magnitude * Mathf.Abs(transform.lossyScale.y));
            Vector3 torque = Vector3.Cross(restCenter.normalized, -localAcceleration) * (response / length);
            float omega = 2 * Mathf.PI * frequency;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120)));
            float h = dt / steps;
            bool strike = false;
            float strikeSpeed = angularVelocity.magnitude;
            for (int i = 0; i < steps; ++i)
            {
                Vector3 before = angle;
                angularVelocity += (torque - omega * omega * angle - 2 * damping * omega * angularVelocity) * h;
                angle += angularVelocity * h;
                float limit = maxAngle * Mathf.Deg2Rad;
                if (angle.magnitude > limit)
                {
                    Vector3 normal = angle.normalized;
                    float outward = Vector3.Dot(angularVelocity, normal);
                    if (outward > 0) angularVelocity -= normal * outward * 1.25f;
                    angle = normal * limit;
                    strike = true;
                }
                if (Vector3.Dot(before, angle) < 0) strike = true;
                strikeSpeed = Mathf.Max(strikeSpeed, angularVelocity.magnitude);
            }
            transform.localRotation = restRotation * Quaternion.AngleAxis(angle.magnitude * Mathf.Rad2Deg, angle.normalized);
            cooldown -= dt;
            if (strike && strikeSpeed > soundSpeedThreshold && cooldown <= 0 && chimes != null && chimes.Length > 0)
            {
                AudioClip clip = chimes[clipIndex++ % chimes.Length];
                if (clip && speaker)
                {
                    speaker.pitch = pitch;
                    speaker.PlayOneShot(clip, volume * Mathf.Clamp01(strikeSpeed / 5));
                    cooldown = minimumSoundInterval;
                }
            }
            previousPosition = position; previousVelocity = velocity; previousBase = basis;
        }
    }
}
