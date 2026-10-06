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
        name: "Select Hunt Target",
        story: "[Agent] selects hunt target into [Target]",
        category: "Action/AI Ghost",
        id: "9e438fbb1b3d4a74bd358dcd65228b9d")]
    public partial class SelectHuntTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            AI_GhostEntity ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            if (ghost == null || !ghost.TrySelectTarget(out GameObject target))
            {
                return Status.Failure;
            }

            Target.Value = target;
            return Status.Success;
        }
    }
}
