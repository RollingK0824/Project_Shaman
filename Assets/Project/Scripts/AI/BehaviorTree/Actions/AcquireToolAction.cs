using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Work;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Acquire Tool",
        story: "[Agent] acquires tool for current routine",
        category: "Action/AI",
        id: "ce9c5f9f8b1d461db5afc0e85a0172de")]
    public partial class AcquireToolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private AI_ToolHandler _handler;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            _handler = Agent.Value.GetComponent<AI_ToolHandler>();
            if (_handler == null)
            {
                return Status.Success;
            }

            return ToStatus(_handler.BeginAcquire());
        }

        protected override Status OnUpdate()
        {
            return _handler == null ? Status.Failure : ToStatus(_handler.TickAcquire());
        }

        protected override void OnEnd()
        {
            if (_handler != null)
            {
                _handler.CancelAcquire();
            }
        }

        private static Status ToStatus(ToolTaskStatus status)
        {
            switch (status)
            {
                case ToolTaskStatus.Success:
                    return Status.Success;
                case ToolTaskStatus.Failure:
                    return Status.Failure;
                default:
                    return Status.Running;
            }
        }
    }
}
