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
        name: "Stop Movement",
        story: "Stops movement of [Agent]",
        category: "Action/AI",
        id: "195544e5033f4cfea655a1f82c246f09")]
    public partial class StopMovementAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            var movable = Agent.Value.GetComponent<INetworkMovable>();
            if (movable != null)
            {
                movable.Stop();
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}
