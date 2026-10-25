using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public class GhostActionSession
    {
        public GhostActionSession(int serial, GhostNightActionData action, IGhostTarget target, float startTime)
        {
            Serial = serial;
            Action = action;
            Target = target;
            StartTime = startTime;
            MovePosition = target.gameObject.transform.position;
        }

        public int Serial { get; }
        public GhostNightActionData Action { get; }
        public IGhostTarget Target { get; }
        public float StartTime { get; }
        public Vector3 MovePosition { get; set; }
        public IGhostCarriable Carried { get; private set; }
        public Vector3 CarriedFrom { get; private set; }
        // Channel time is kept as "elapsed at the last mark + time since", so it can pause and rewind
        public float ChannelDuration { get; private set; } = -1f;
        public float ChannelPausedUntil { get; private set; } = float.NegativeInfinity;
        private float _channelElapsedAtMark;
        private float _channelMarkTime;

        public GhostActionShape Shape => Action.Shape;
        public bool IsCarrying => Carried != null;
        public bool IsChanneling => ChannelDuration >= 0f;
        public string TargetName => IsTargetAlive ? Target.gameObject.name : "(gone)";
        public bool IsTargetAlive => Target != null && !(Target is Object unityObject && unityObject == null);

        public void SetCarried(IGhostCarriable carried, Vector3 from)
        {
            Carried = carried;
            CarriedFrom = from;
        }

        public IGhostCarriable ReleaseCarried()
        {
            IGhostCarriable carried = Carried;
            Carried = null;
            return carried;
        }

        public void BeginChannel(float now, float duration)
        {
            ChannelDuration = Mathf.Max(0f, duration);
            ChannelPausedUntil = float.NegativeInfinity;
            _channelElapsedAtMark = 0f;
            _channelMarkTime = now;
        }

        public void EndChannel()
        {
            ChannelDuration = -1f;
            ChannelPausedUntil = float.NegativeInfinity;
        }

        public float ChannelElapsed(float now)
        {
            if (!IsChanneling)
            {
                return 0f;
            }

            return _channelElapsedAtMark + Mathf.Max(0f, now - Mathf.Max(_channelMarkTime, ChannelPausedUntil));
        }

        public bool IsChannelDone(float now)
        {
            return IsChanneling && ChannelElapsed(now) >= ChannelDuration;
        }

        // 0..1, 1 once the channel time has passed
        public float ChannelProgress(float now)
        {
            if (!IsChanneling)
            {
                return 0f;
            }

            return ChannelDuration <= 0f ? 1f : Mathf.Clamp01(ChannelElapsed(now) / ChannelDuration);
        }

        public void RewindChannel(float now, float seconds)
        {
            float elapsed = ChannelElapsed(now);
            _channelMarkTime = now;
            _channelElapsedAtMark = Mathf.Max(0f, elapsed - Mathf.Max(0f, seconds));
        }

        public void PauseChannel(float now, float seconds)
        {
            _channelElapsedAtMark = ChannelElapsed(now);
            _channelMarkTime = now;
            ChannelPausedUntil = Mathf.Max(ChannelPausedUntil, now + Mathf.Max(0f, seconds));
        }

        public override string ToString()
        {
            return $"#{Serial} {Action.ActionId} {Action.Verb}/{Shape} → {TargetName}";
        }
    }
}
