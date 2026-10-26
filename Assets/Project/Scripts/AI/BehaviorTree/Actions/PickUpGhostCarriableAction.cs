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
        story: "[Agent] channels then picks up command target",
        category: "Action/AI Ghost",
        id: "6dee76619565409ebc0ed0fd8dda0fba")]
    public partial class PickUpGhostCarriableAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private AI_GhostEntity _ghost;

        // The channel comes before the pick-up so a player can stop the ghost before the object is gone
        protected override Status OnStart()
        {
            _ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;

            if (_ghost == null || !_ghost.BeginChannel())
            {
                return Status.Failure;
            }

            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            if (_ghost == null || !_ghost.TickChannel(out bool finished))
            {
                return Status.Failure;
            }

            if (!finished)
            {
                return Status.Running;
            }

            return _ghost.TryPickUpTarget() ? Status.Success : Status.Failure;
        }
    }
}
