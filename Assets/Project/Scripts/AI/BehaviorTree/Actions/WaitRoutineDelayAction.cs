using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Wait Routine Delay",
        story: "[Agent] stands still for routine start delay",
        category: "Action/AI",
        id: "0eb705eed43b4d42b215de1c6b5ad92b")]
    public partial class WaitRoutineDelayAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private float _endTime;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            AI_Core core = Agent.Value.GetComponent<AI_Core>();
            if (core == null || core.CurrentRoutine.StartDelaySeconds <= 0f)
            {
                return Status.Success;
            }

            float delay = core.CurrentRoutine.StartDelaySeconds;
            _endTime = Time.time + delay;
            core.SetState(AIState.Idle);
            AILog.Log(AILog.GHOST, core.LogId, $"Standing still {delay:F1}s before routine");
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            return Time.time >= _endTime ? Status.Success : Status.Running;
        }
    }
}
