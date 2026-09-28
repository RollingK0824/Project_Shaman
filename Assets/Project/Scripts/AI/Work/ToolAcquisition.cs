using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public class ToolAcquisition
    {
        private const float ARRIVE_DISTANCE = 1.2f;

        public enum State
        {
            Idle,
            MovingToCandidate,
            MovingToTool
        }

        private readonly AI_ToolHandler _owner;
        private readonly List<Vector3> _candidates = new List<Vector3>();

        private ToolItem _targetTool;
        private bool _hasRaisedFirstStress;
        private float _deadline;

        public State CurrentState { get; private set; }
        public string RequiredToolType { get; private set; }
        public int CandidateIndex { get; private set; }
        public int CandidateCount => _candidates.Count;

        private string Id => _owner.VillagerId;
        private AIBehaviourConfig Config => _owner.Context.Config;
        private float Now => _owner.Context.GameNow;
        private Vector3 Position => _owner.transform.position;

        public ToolAcquisition(AI_ToolHandler owner)
        {
            _owner = owner;
        }

        public ToolTaskStatus Begin(ResolvedRoutine routine)
        {
            RequiredToolType = routine.ToolTypeId;

            if (string.IsNullOrEmpty(RequiredToolType))
            {
                return ToolTaskStatus.Success;
            }

            if (_owner.IsHoldingType(RequiredToolType))
            {
                AILog.Log(AILog.TOOL, Id, $"Already holding {_owner.HeldTool.InstanceId}");
                return ToolTaskStatus.Success;
            }

            if (_owner.IsHolding)
            {
                _owner.Drop(Position, "switch tool");
            }

            float budget = Mathf.Max(1f, routine.EndTime - routine.StartTime) * Config.ToolSearchTimeRatio;
            _deadline = Now + budget;
            _hasRaisedFirstStress = false;

            _owner.Perception.ScanTools(Position, Config.ToolSightRadius);

            string placeId = routine.Source != null ? routine.Source.PlaceId : null;
            IReadOnlyList<ToolSighting> sightings = _owner.Memory.ToolSightings.Get(RequiredToolType);
            ToolSearchPlanner.BuildCandidates(RequiredToolType, sightings, placeId, _owner.Context.ToolUsePlaces, Position, ARRIVE_DISTANCE * 2f, Now, Config.ToolSightInterval * 3f, _candidates);
            LogHeldByOthers(sightings);

            AILog.Log(AILog.TOOL, Id, $"Acquire '{RequiredToolType}' start (candidates={_candidates.Count}, budget={budget:F1}s)");

            ToolItem visible = ToolRegistry.FindAvailableNear(RequiredToolType, Position, Config.ToolSightRadius, Id);
            if (visible != null && TargetTool(visible))
            {
                return ToolTaskStatus.Running;
            }

            CandidateIndex = -1;
            return MoveToNextCandidate();
        }

        public ToolTaskStatus Tick()
        {
            if (Now > _deadline && CurrentState != State.MovingToTool)
            {
                ToolItem lastChance = ToolRegistry.FindAvailableNear(RequiredToolType, Position, Config.ToolSightRadius, Id);
                if (lastChance == null || !TargetTool(lastChance))
                {
                    return Fail($"search time exceeded ({Now:F1}s > {_deadline:F1}s)");
                }

                AILog.Log(AILog.TOOL, Id, $"Search time exceeded but {lastChance.InstanceId} is in sight, going to pick up");
            }

            switch (CurrentState)
            {
                case State.MovingToTool:
                    return TickMovingToTool();
                case State.MovingToCandidate:
                    return TickMovingToCandidate();
                default:
                    return ToolTaskStatus.Failure;
            }
        }

        public void Cancel()
        {
            ReleaseTarget();
            CurrentState = State.Idle;
        }

        private ToolTaskStatus TickMovingToTool()
        {
            if (_targetTool == null || !_targetTool.IsAvailableFor(Id))
            {
                AILog.Log(AILog.TOOL, Id, "Target tool taken by someone else, rescanning");
                ReleaseTarget();
                return RescanOrNext(Position);
            }

            if (!IsWithinPickupRange(_targetTool.Position))
            {
                return ToolTaskStatus.Running;
            }

            ToolItem tool = _targetTool;
            _targetTool = null;

            if (!_owner.TryPickUp(tool))
            {
                return RescanOrNext(Position);
            }

            CurrentState = State.Idle;
            _owner.Movable.Stop();
            return ToolTaskStatus.Success;
        }

        private ToolTaskStatus TickMovingToCandidate()
        {
            Vector3 candidate = _candidates[CandidateIndex];
            float distance = Vector3.Distance(Flat(Position), Flat(candidate));

            if (distance > ARRIVE_DISTANCE && !_owner.Movable.HasReachedDestination)
            {
                ToolItem spotted = ToolRegistry.FindAvailableNear(RequiredToolType, Position, Config.ToolSightRadius * 0.5f, Id);
                if (spotted != null)
                {
                    TargetTool(spotted);
                }

                return ToolTaskStatus.Running;
            }

            _owner.Perception.ScanTools(candidate, Config.ToolSightRadius);
            return RescanOrNext(candidate);
        }

        private ToolTaskStatus RescanOrNext(Vector3 center)
        {
            ToolItem found = ToolRegistry.FindAvailableNear(RequiredToolType, center, Config.ToolSightRadius, Id);
            if (found != null && TargetTool(found))
            {
                return ToolTaskStatus.Running;
            }

            if (!_hasRaisedFirstStress)
            {
                _hasRaisedFirstStress = true;
                AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' not at expected place", 1);
            }

            return MoveToNextCandidate();
        }

        private ToolTaskStatus MoveToNextCandidate()
        {
            CandidateIndex++;

            if (CandidateIndex >= _candidates.Count)
            {
                AILog.Log(AILog.STUB, Id, $"Inquiry to nearby villagers for '{RequiredToolType}' skipped (interaction branch)");
                AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' search failed", 2);
                AIEvents.RaiseRequest(Id, "FindTool", RequiredToolType);
                return Fail("all candidates checked");
            }

            CurrentState = State.MovingToCandidate;
            _owner.Movable.MoveTo(_candidates[CandidateIndex]);
            AILog.Log(AILog.TOOL, Id, $"Searching '{RequiredToolType}' at candidate {CandidateIndex + 1}/{_candidates.Count} {_candidates[CandidateIndex]}");
            return ToolTaskStatus.Running;
        }

        private bool TargetTool(ToolItem tool)
        {
            if (!tool.TryReserve(Id))
            {
                return false;
            }

            if (_targetTool != tool)
            {
                ReleaseTarget();
            }

            _targetTool = tool;
            CurrentState = State.MovingToTool;
            _owner.Movable.MoveTo(tool.Position);
            AILog.Log(AILog.TOOL, Id, $"Reserved {tool.InstanceId}, moving to pick up");
            return true;
        }

        private ToolTaskStatus Fail(string reason)
        {
            AILog.Log(AILog.TOOL, Id, $"Acquire '{RequiredToolType}' failed ({reason}, checked {Mathf.Max(0, CandidateIndex)}/{_candidates.Count} candidates)");
            ReleaseTarget();
            CurrentState = State.Idle;
            _owner.Core.FailCurrentRoutine($"tool '{RequiredToolType}' not found");
            return ToolTaskStatus.Failure;
        }

        private void LogHeldByOthers(IReadOnlyList<ToolSighting> sightings)
        {
            foreach (ToolSighting sighting in sightings)
            {
                if (!string.IsNullOrEmpty(sighting.HolderId) && sighting.HolderId != Id)
                {
                    AILog.Log(AILog.TOOL, Id, $"Knows {sighting.InstanceId} is used by {sighting.HolderId}, searching elsewhere");
                }
            }
        }

        private bool IsWithinPickupRange(Vector3 toolPosition)
        {
            float stopping = _owner.Agent != null ? _owner.Agent.stoppingDistance : 0f;
            float range = Mathf.Max(Config.ToolPickupDistance, stopping + 0.5f);
            return Vector3.Distance(Flat(Position), Flat(toolPosition)) <= range || _owner.Movable.HasReachedDestination;
        }

        private void ReleaseTarget()
        {
            if (_targetTool != null)
            {
                _targetTool.CancelReservation(Id);
                _targetTool = null;
            }
        }

        private static Vector3 Flat(Vector3 position)
        {
            position.y = 0f;
            return position;
        }
    }
}
