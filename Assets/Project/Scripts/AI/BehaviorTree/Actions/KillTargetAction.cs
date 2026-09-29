using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interfaces;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Kill Target",
        story: "[Agent] kills [Target]",
        category: "Action/AI Ghost",
        id: "6ebf5eba6cc64cc7abbd4b21e3b64257")]
    public partial class KillTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            AI_GhostEntity ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            if (ghost == null || !ghost.TryKill(Target.Value))
            {
                return Status.Failure;
            }

            Target.Value = null;
            return Status.Success;
        }
    }
}
