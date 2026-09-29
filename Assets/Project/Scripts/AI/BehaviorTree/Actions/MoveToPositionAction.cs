using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Interfaces;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Move To Position",
        story: "Moves [Agent] to [TargetPosition]",
        category: "Action/AI",
        id: "84c8f45f3c454dec861e6843c9c23794")]
    public partial class MoveToPositionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;
        [SerializeReference] public BlackboardVariable<float> ArriveDistance = new BlackboardVariable<float>(0f);

        private INetworkMovable movable;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            movable = Agent.Value.GetComponent<INetworkMovable>();
            if (movable == null) return Status.Failure;

            movable.MoveTo(TargetPosition.Value);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (movable == null) return Status.Failure;

            if (movable.HasReachedDestination || IsWithinArriveDistance())
            {
                return Status.Success;
            }

            return Status.Running;
        }

        private bool IsWithinArriveDistance()
        {
            float arrive = ArriveDistance != null ? ArriveDistance.Value : 0f;
            if (arrive <= 0f)
            {
                return false;
            }

            Vector3 offset = Agent.Value.transform.position - TargetPosition.Value;
            offset.y = 0f;
            return offset.sqrMagnitude <= arrive * arrive;
        }

        protected override void OnEnd()
        {
            if (movable != null && !movable.HasReachedDestination)
            {
                movable.Stop();
            }
        }
    }
}
