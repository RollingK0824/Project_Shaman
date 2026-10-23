using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Observation;
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
        private const float TURN_INTERVAL_MIN = 4f;
        private const float TURN_INTERVAL_MAX = 7f;
        private const float MUTTER_SECONDS = 5f;
        private static readonly string[] MUTTER_LINES = { "…아직 때가 아니여…", "조금만… 조금만 더…", "저기 누가 서 있는디…" };

        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private AI_Core _core;
        private float _endTime;
        private float _nextTurnTime;
        private float _mutterTime;
        private bool _hasMuttered;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                return Status.Failure;
            }

            _core = Agent.Value.GetComponent<AI_Core>();
            if (_core == null || !_core.TryConsumeStartDelay(out float delay))
            {
                return Status.Success;
            }

            float now = Time.time;
            _endTime = now + delay;
            _nextTurnTime = now + UnityEngine.Random.Range(TURN_INTERVAL_MIN, TURN_INTERVAL_MAX);
            _mutterTime = now + delay * UnityEngine.Random.Range(0.3f, 0.7f);
            _hasMuttered = false;

            _core.SetState(AIState.Idle);
            _core.TriggerSymptom(SymptomType.StareIntoVoid);
            AILog.Log(AILog.GHOST, _core.LogId, $"Standing still {delay:F1}s before routine (staring, turning, muttering)");
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            float now = Time.time;
            if (now >= _endTime)
            {
                return Status.Success;
            }

            if (now >= _nextTurnTime)
            {
                _nextTurnTime = now + UnityEngine.Random.Range(TURN_INTERVAL_MIN, TURN_INTERVAL_MAX);
                _core.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            }

            if (!_hasMuttered && now >= _mutterTime)
            {
                _hasMuttered = true;
                Mutter();
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            _core = null;
        }

        private void Mutter()
        {
            ObservableRegistry.Register(_core.LogId, _core.transform, ObservationVerbs.TALK_NONSENSE, null, MUTTER_SECONDS);

            if (_core.TryGetComponent(out AI_Conversation conversation))
            {
                conversation.Say(MUTTER_LINES[UnityEngine.Random.Range(0, MUTTER_LINES.Length)], "time anomaly mutter");
            }
        }
    }
}
