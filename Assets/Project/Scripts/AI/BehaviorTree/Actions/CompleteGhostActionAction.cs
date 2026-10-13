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
        name: "Complete Ghost Action",
        story: "[Agent] completes current command",
        category: "Action/AI Ghost",
        id: "41513829376545cc8fc5b985a908c8fe")]
    public partial class CompleteGhostActionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            AI_GhostEntity ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            return ghost != null && ghost.CompleteCommand() ? Status.Success : Status.Failure;
        }
    }
}
