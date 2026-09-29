using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Work;

namespace ProjectShaman.AI.BehaviorTree.Conditions
{
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Can Continue Tool Search",
        category: "Conditions/AI Tool",
        story: "[Agent] can continue tool search",
        id: "a913a19a506244dc889572cc059c0102")]
    public partial class CanContinueToolSearchCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        public override bool IsTrue()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            return handler != null && handler.Search.CanContinue();
        }
    }
}
