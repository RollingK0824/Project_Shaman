using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Face Target",
        story: "[Agent] turns to face [LookPoint]",
        category: "Action/AI Common",
        id: "222faedfbcfe481c9a6755d73e325014")]
    public partial class FaceTargetAction : Action
    {
        private const float FACING_ANGLE = 5f;
        private const float MAX_TURN_SECONDS = 2f;

        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> LookPoint;
        [SerializeReference] public BlackboardVariable<float> TurnSpeed = new BlackboardVariable<float>(360f);

        private float _endTime;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            _endTime = Time.time + MAX_TURN_SECONDS;
            return TryGetDirection(out _) ? Status.Running : Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            if (!TryGetDirection(out Vector3 direction))
            {
                return Status.Success;
            }

            Transform agentTransform = Agent.Value.transform;
            Quaternion target = Quaternion.LookRotation(direction);
            agentTransform.rotation = Quaternion.RotateTowards(agentTransform.rotation, target, TurnSpeed.Value * Time.deltaTime);

            if (Quaternion.Angle(agentTransform.rotation, target) <= FACING_ANGLE || Time.time >= _endTime)
            {
                return Status.Success;
            }

            return Status.Running;
        }

        private bool TryGetDirection(out Vector3 direction)
        {
            direction = LookPoint.Value - Agent.Value.transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f;
        }
    }
}
