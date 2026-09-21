using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Defines;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set AI State",
        story: "Sets [Agent] state to [NewState]",
        category: "Action/AI",
        id: "f4fb92e833904a7a9f93f148ce5ab434")]
    public partial class SetAIStateAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<AIState> NewState;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;

            var core = Agent.Value.GetComponent<AI_Core>();
            if (core != null)
            {
                core.SetState(NewState.Value);
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}
