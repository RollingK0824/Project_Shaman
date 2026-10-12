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
        name: "Select Ghost Target",
        story: "[Agent] selects target of current command into [Target]",
        category: "Action/AI Ghost",
        id: "8719fc26123741aa988ccfa79abe9ffa")]
    public partial class SelectGhostTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        protected override Status OnStart()
        {
            IGhostActionAgent agent = Agent.Value != null ? Agent.Value.GetComponent<IGhostActionAgent>() : null;
            if (agent == null)
            {
                return Status.Failure;
            }

            GhostNightCommand command = agent.Command;
            GhostActionContext context = agent.CreateContext();
            IGhostTarget target = GhostTargetRegistry.Find(command.Query, Agent.Value.transform.position, context);

            if (target == null)
            {
                Target.Value = null;
                AILog.Log(AILog.GHOST, context.GhostId, $"No target for {command}");
                return Status.Failure;
            }

            Target.Value = target.gameObject;
            AILog.Log(AILog.GHOST, context.GhostId, $"Target {target.Kind} {target.gameObject.name} selected for {command.Verb}");
            return Status.Success;
        }
    }
}
