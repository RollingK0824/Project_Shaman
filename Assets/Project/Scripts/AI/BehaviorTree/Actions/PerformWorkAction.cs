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
        name: "Perform Work",
        story: "[Agent] performs current work",
        category: "Action/AI",
        id: "75f01ff32c8d45e2955c25ecb55ce4a5")]
    public partial class PerformWorkAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private AI_WorkPerformer _performer;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            _performer = Agent.Value.GetComponent<AI_WorkPerformer>();
            if (_performer == null || !_performer.BeginWork())
            {
                return Status.Failure;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_performer == null)
            {
                return Status.Failure;
            }

            _performer.TickWork(Time.deltaTime);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (_performer != null)
            {
                _performer.EndWork();
            }
        }
    }
}
