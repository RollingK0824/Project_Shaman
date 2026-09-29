using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Work;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Raise Tool Search Failure",
        story: "[Agent] gives up tool search (stress, request, routine fail)",
        category: "Action/AI Tool",
        id: "f8d3f17816694302b2d43968e4b34076")]
    public partial class RaiseToolSearchFailureAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            if (handler != null && handler.Search.HasRequiredTool())
            {
                ProjectShaman.AI.Core.AILog.Warn(ProjectShaman.AI.Core.AILog.TOOL, $"{handler.VillagerId} reached search failure while holding required tool, ignored (check Repeat While 'Return Failure On Condition Fail')");
                return Status.Success;
            }

            if (handler != null)
            {
                handler.Search.RaiseFailure();
                handler.RefreshSearchDebug();
            }

            return Status.Failure;
        }
    }
}
