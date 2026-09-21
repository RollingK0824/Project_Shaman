using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Interfaces;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Move To Random Position",
        story: "Moves [Agent] to random point within [Radius]",
        category: "Action/AI",
        id: "f4d20fb9da834f94970ffb40b4bdb5fd")]
    public partial class MoveToRandomPositionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<float> Radius = new BlackboardVariable<float>(5f);

        private INetworkMovable movable;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            movable = Agent.Value.GetComponent<INetworkMovable>();
            if (movable == null) return Status.Failure;

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * Radius.Value;
            Vector3 targetPos = Agent.Value.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, Radius.Value, NavMesh.AllAreas))
            {
                movable.MoveTo(hit.position);
                return Status.Running;
            }

            return Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (movable == null) return Status.Failure;

            if (movable.HasReachedDestination)
            {
                return Status.Success;
            }

            return Status.Running;
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
