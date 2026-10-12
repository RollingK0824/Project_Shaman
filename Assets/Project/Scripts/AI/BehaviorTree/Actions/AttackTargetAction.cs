using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Ghost;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Attack Target",
        story: "[Agent] attacks [Target]",
        category: "Action/AI Ghost",
        id: "6be604bf294347a39d99f732dafd5bb2")]
    public partial class AttackTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            IGhostActionAgent agent = Agent.Value != null ? Agent.Value.GetComponent<IGhostActionAgent>() : null;
            IGhostAttackable attackable = Target.Value != null ? Target.Value.GetComponent<IGhostAttackable>() : null;

            if (agent == null || attackable == null)
            {
                return Status.Failure;
            }

            GhostActionContext context = agent.CreateContext();
            if (!GhostTargetRegistry.IsCandidate(attackable, agent.Command.Query, context))
            {
                AILog.Log(AILog.GHOST, context.GhostId, $"Attack on {Target.Value.name} cancelled (target no longer valid)");
                return Status.Failure;
            }

            attackable.ReceiveGhostAttack(context);
            AILog.Log(AILog.GHOST, context.GhostId, $"Attacked {attackable.Kind} {Target.Value.name}");
            Target.Value = null;
            return Status.Success;
        }
    }
}
