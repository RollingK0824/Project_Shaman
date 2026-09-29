using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interfaces;
using Action = Unity.Behavior.Action;

namespace ProjectShaman.AI.BehaviorTree.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Chase Target",
        story: "[Agent] chases [Target] inside radius",
        category: "Action/AI Ghost",
        id: "63298d9dd26349e29a729d679b77fb42")]
    public partial class ChaseTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        private const float REPATH_INTERVAL = 0.25f;

        private AI_GhostEntity _ghost;
        private INetworkMovable _movable;
        private float _nextRepathTime;

        protected override Status OnStart()
        {
            _ghost = Agent.Value != null ? Agent.Value.GetComponent<AI_GhostEntity>() : null;
            _movable = Agent.Value != null ? Agent.Value.GetComponent<INetworkMovable>() : null;

            if (_ghost == null || _movable == null || !_ghost.IsTargetValid(Target.Value))
            {
                return Status.Failure;
            }

            _nextRepathTime = 0f;
            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            if (!_ghost.IsTargetValid(Target.Value))
            {
                ProjectShaman.AI.Core.AILog.Log(ProjectShaman.AI.Core.AILog.GHOST, _ghost.GhostId, "Target lost (dead or left radius)");
                return Status.Failure;
            }

            Vector3 offset = Target.Value.transform.position - Agent.Value.transform.position;
            offset.y = 0f;

            if (offset.magnitude <= _ghost.KillDistance)
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
