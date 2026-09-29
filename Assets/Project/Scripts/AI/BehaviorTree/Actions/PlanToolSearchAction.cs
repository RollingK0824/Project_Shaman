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
        name: "Plan Tool Search",
        story: "[Agent] plans tool search",
        category: "Action/AI Tool",
        id: "ce7889bdcca84a5395b7bf10d16b8fe0")]
    public partial class PlanToolSearchAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            if (handler == null)
            {
                return Status.Failure;
            }

            handler.Search.Plan();
            handler.RefreshSearchDebug();
            return Status.Success;
        }
    }
}
