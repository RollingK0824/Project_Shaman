using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Ghost;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Pick Up Ghost Carriable",
        story: "[Agent] picks up command target",
        category: "Action/AI Ghost",
        id: "6dee76619565409ebc0ed0fd8dda0fba")]
    public partial class PickUpGhostCarriableAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            AI_GhostEntity ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            return ghost != null && ghost.TryPickUpTarget() ? Status.Success : Status.Failure;
        }
    }
}
