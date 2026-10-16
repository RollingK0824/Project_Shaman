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
        name: "Channel Ghost Action",
        story: "[Agent] channels current command",
        category: "Action/AI Ghost",
        id: "1d26d417ecc0499983044683049c6ee5")]
    public partial class ChannelGhostActionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private AI_GhostEntity _ghost;

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

            return finished ? Status.Success : Status.Running;
        }
    }
}
