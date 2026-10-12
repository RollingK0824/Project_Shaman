using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Interfaces;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Move To Target",
        story: "Moves [Agent] to [Target] within [ArriveDistance]",
        category: "Action/AI",
        id: "4f1abc2da27e46e8872c5f4d31c8e72d")]
    public partial class MoveToTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> ArriveDistance = new BlackboardVariable<float>(1.2f);

        private const float REPATH_INTERVAL = 0.25f;

        private INetworkMovable _movable;
        private float _nextRepathTime;

        protected override Status OnStart()
        {
            _movable = Agent.Value != null ? Agent.Value.GetComponent<INetworkMovable>() : null;

            if (_movable == null || Target.Value == null)
            {
                return Status.Failure;
            }

            _nextRepathTime = 0f;
            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            if (_movable == null || Target.Value == null)
            {
                return Status.Failure;
            }

            Vector3 offset = Target.Value.transform.position - Agent.Value.transform.position;
            offset.y = 0f;

            float arrive = ArriveDistance != null ? ArriveDistance.Value : 0f;
            if (offset.sqrMagnitude <= arrive * arrive)
            {
                _movable.Stop();
                return Status.Success;
            }

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + REPATH_INTERVAL;
                _movable.MoveTo(Target.Value.transform.position);
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (_movable != null)
            {
                _movable.Stop();
            }
        }
    }
}
