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
        name: "Tidy Tool",
        story: "[Agent] tidies held tool before going home",
        category: "Action/AI",
        id: "558c438249104e7a8e2e3044da09d8b8")]
    public partial class TidyToolAction : Action
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
            return _handler == null ? Status.Success : ToStatus(_handler.BeginTidy());
        }

        protected override Status OnUpdate()
        {
            return _handler == null ? Status.Success : ToStatus(_handler.TickTidy());
        }

        protected override void OnEnd()
        {
            if (_handler != null)
            {
                _handler.CancelTidy();
            }
        }

        private static Status ToStatus(ToolTaskStatus status)
        {
            return status == ToolTaskStatus.Running ? Status.Running : status == ToolTaskStatus.Success ? Status.Success : Status.Failure;
        }
    }
}
