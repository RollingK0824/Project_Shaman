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
        name: "Pick Up Tool",
        story: "[Agent] picks up reserved tool",
        category: "Action/AI Tool",
        id: "63ea3edf2f424dd0a7e8e197486d9e42")]
    public partial class PickUpToolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            bool isPickedUp = handler != null && handler.Search.TryPickUpTarget();

            if (handler != null)
            {
                handler.RefreshSearchDebug();
            }

            return isPickedUp ? Status.Success : Status.Failure;
        }
    }
}
