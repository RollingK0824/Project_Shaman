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
        story: "[Target] is valid for [Agent]",
        id: "583f7fe169a543599273fa553c749f61")]
    public partial class IsGhostTargetValidCondition : Unity.Behavior.Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        public override bool IsTrue()
        {
            IGhostActionAgent agent = Agent.Value != null ? Agent.Value.GetComponent<IGhostActionAgent>() : null;
            IGhostTarget target = Target.Value != null ? Target.Value.GetComponent<IGhostTarget>() : null;

            if (agent == null || target == null)
            {
                return false;
            }

            return GhostTargetRegistry.IsCandidate(target, agent.Command.Query, agent.CreateContext());
        }
    }
}
