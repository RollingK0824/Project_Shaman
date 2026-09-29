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
        name: "Look For Tool",
        story: "[Agent] looks for tool nearby and writes [ToolPosition]",
        category: "Action/AI Tool",
        id: "b9836b4d159d40608c9f192f4b30451b")]
    public partial class LookForToolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> ToolPosition;

        protected override Status OnStart()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            if (handler == null || !handler.Search.TryLookForTool(out Vector3 position))
            {
                return Status.Failure;
            }

            ToolPosition.Value = position;
            return Status.Success;
        }
    }
}
