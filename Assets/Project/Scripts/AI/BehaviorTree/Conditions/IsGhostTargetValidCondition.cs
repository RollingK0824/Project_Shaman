using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.BehaviorTree.Conditions
{
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Is Ghost Target Valid",
        category: "Conditions/AI Ghost",
        story: "[Agent] command target is valid",
        id: "583f7fe169a543599273fa553c749f61")]
    public partial class IsGhostTargetValidCondition : Unity.Behavior.Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        public override bool IsTrue()
        {
            AI_GhostEntity ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            return ghost != null && ghost.IsTargetValid();
        }
    }
}
