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
        // Channel time is kept as "elapsed at the last mark + time since", so it can pause and slow down
        public float ChannelDuration { get; private set; } = -1f;
        public float ChannelPausedUntil { get; private set; } = float.NegativeInfinity;
        public float ChannelSlowRate { get; private set; } = 1f;
        public float ChannelSlowUntil { get; private set; } = float.NegativeInfinity;
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
            _channelElapsedAtMark = 0f;
            _channelMarkTime = now;
            // A pause or slow already on the ghost carries over into the next channel
        }

        public void EndChannel()
        {
            ChannelDuration = -1f;
        }

        public float ChannelElapsed(float now)
        {
            if (!IsChanneling)
            {
                return 0f;
            }

            return (float)ElapsedSince(_channelElapsedAtMark, _channelMarkTime, ChannelPausedUntil, ChannelSlowRate, ChannelSlowUntil, now);
        }

        // Shared with the network copy: nothing counts until the pause ends, then time runs at rate until slowUntil
        public static double ElapsedSince(double elapsedAtMark, double markTime, double pausedUntil, double slowRate, double slowUntil, double now)
        {
            double from = System.Math.Max(markTime, pausedUntil);
            if (now <= from)
            {
                return elapsedAtMark;
            }

            double slowEnd = System.Math.Min(System.Math.Max(slowUntil, from), now);
            return elapsedAtMark + (slowEnd - from) * slowRate + (now - slowEnd);
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

        public void PauseChannel(float now, float seconds)
        {
            Mark(now);
            ChannelPausedUntil = Mathf.Max(ChannelPausedUntil, now + Mathf.Max(0f, seconds));
        }

        // The channel runs at rate (0..1) for a while
        public void SlowChannel(float now, float rate, float seconds)
        {
            Mark(now);
            ChannelSlowRate = Mathf.Clamp01(rate);
            ChannelSlowUntil = now + Mathf.Max(0f, seconds);
        }

        private void Mark(float now)
        {
            _channelElapsedAtMark = ChannelElapsed(now);
            _channelMarkTime = now;
        }

        public override string ToString()
        {
            return $"#{Serial} {Action.ActionId} {Action.Verb}/{Shape} → {TargetName}";
        }
    }
}
