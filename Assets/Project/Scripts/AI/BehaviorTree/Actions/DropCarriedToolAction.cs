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
        name: "Drop Carried Tool",
        story: "[Agent] drops carried tool at [HomePosition]",
        category: "Action/AI",
        id: "c00e7bd3e6a34e759d4c71150c0e6f89")]
    public partial class DropCarriedToolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> HomePosition;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            AI_ToolHandler handler = Agent.Value.GetComponent<AI_ToolHandler>();
            if (handler != null)
            {
                handler.DropCarriedAtHome(HomePosition.Value);
            }

            return Status.Success;
        }
    }
}
