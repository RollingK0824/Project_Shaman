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
        name: "Select Next Search Target",
        story: "[Agent] selects next search target into [SearchTarget]",
        category: "Action/AI Tool",
        id: "beeaaefacaa741558e49c4e2a75ff607")]
    public partial class SelectNextSearchTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> SearchTarget;

        protected override Status OnStart()
        {
            AI_ToolHandler handler = Agent.Value != null ? Agent.Value.GetComponent<AI_ToolHandler>() : null;
            if (handler == null || !handler.Search.TrySelectNextTarget(out Vector3 target))
            {
                return Status.Failure;
            }

            SearchTarget.Value = target;
            handler.RefreshSearchDebug();
            return Status.Success;
        }
    }
}
