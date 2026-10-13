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
        public float ChannelEndTime { get; private set; } = -1f;

        public GhostActionShape Shape => Action.Shape;
        public bool IsCarrying => Carried != null;
        public bool IsChanneling => ChannelEndTime >= 0f;
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

        public void BeginChannel(float now)
        {
            ChannelEndTime = now + Action.Duration;
        }

        public override string ToString()
        {
            return $"#{Serial} {Action.ActionId} {Action.Verb}/{Shape} → {TargetName}";
        }
    }
}
