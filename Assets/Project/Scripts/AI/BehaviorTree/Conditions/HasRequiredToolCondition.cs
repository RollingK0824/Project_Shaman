using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Work;

namespace ProjectShaman.AI.BehaviorTree.Conditions
{
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Has Required Tool",
        category: "Conditions/AI Tool",
        story: "[Agent] has required tool",
        id: "8630d1abdaf14f00a40622328f1e6053")]
    public partial class HasRequiredToolCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        public override bool IsTrue()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            return handler == null || handler.Search.HasRequiredTool();
        }
    }
}
