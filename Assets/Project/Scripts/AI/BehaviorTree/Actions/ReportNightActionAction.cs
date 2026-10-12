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
        name: "Report Night Action",
        story: "[Agent] reports night action succeeded [Succeeded]",
        category: "Action/AI Ghost",
        id: "583aec208c4b473e9ef4888aa2d44388")]
    public partial class ReportNightActionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<bool> Succeeded = new BlackboardVariable<bool>(true);

        protected override Status OnStart()
        {
            IGhostActionAgent agent = Agent.Value != null ? Agent.Value.GetComponent<IGhostActionAgent>() : null;
            if (agent == null)
            {
                return Status.Failure;
            }

            agent.ReportActionEnded(Succeeded != null && Succeeded.Value);
            return Status.Success;
        }
    }
}
