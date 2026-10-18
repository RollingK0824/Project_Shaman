using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Interaction;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Complete Interaction",
        story: "[Agent] completes interaction [InteractionSerial]",
        category: "Action/AI",
        id: "3c5aff3e561f43548877b44332fb021f")]
    public partial class CompleteInteractionAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<int> InteractionSerial;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            AI_Interaction interaction = Agent.Value.GetComponent<AI_Interaction>();
            if (interaction == null)
            {
                return Status.Failure;
            }

            return interaction.CompleteActive(InteractionSerial.Value) ? Status.Success : Status.Failure;
        }
    }
}
