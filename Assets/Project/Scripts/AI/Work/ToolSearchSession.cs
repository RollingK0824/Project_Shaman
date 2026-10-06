using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    public class ToolSearchSession
    {
        private readonly AI_ToolHandler _owner;
        private readonly List<Vector3> _candidates = new List<Vector3>();

        private ToolItem _targetTool;
        private float _deadline;
        private bool _hasRaisedFirstStress;
        private bool _isLookPendingAfterArrival;

        public string RequiredToolType { get; private set; }
        public int CandidateIndex { get; private set; } = -1;
        public int CandidateCount => _candidates.Count;
        public bool IsActive { get; private set; }

        private string Id => _owner.VillagerId;
        private AIBehaviourConfig Config => _owner.Context.Config;
        private float Now => _owner.Context.GameNow;
        private Vector3 Position => _owner.transform.position;

        public ToolSearchSession(AI_ToolHandler owner)
        {
            _owner = owner;
        }

        public bool HasRequiredTool()
        {
            string required = _owner.Core.CurrentRoutine.ToolTypeId;
            return string.IsNullOrEmpty(required) || _owner.IsHoldingType(required);
        }

        public void Plan()
        {
            Cancel();

            ResolvedRoutine routine = _owner.Core.CurrentRoutine;
            RequiredToolType = routine.ToolTypeId;

            if (_owner.IsHolding && !_owner.IsHoldingType(RequiredToolType))
            {
                _owner.Drop(Position, "switch tool");
            }

            float budget = Mathf.Max(1f, routine.EndTime - routine.StartTime) * Config.ToolSearchTimeRatio;
            _deadline = Now + budget;
            _hasRaisedFirstStress = false;
            _isLookPendingAfterArrival = false;
            CandidateIndex = -1;
            IsActive = true;

            _owner.Perception.ScanTools(Position, Config.ToolSightRadius);

            string placeId = routine.Source != null ? routine.Source.PlaceId : null;
            IReadOnlyList<ToolSighting> sightings = _owner.Memory.ToolSightings.Get(RequiredToolType);
            ToolSearchPlanner.BuildCandidates(RequiredToolType, sightings, placeId, _owner.Context.ToolUsePlaces, Position, 2.4f, Now, Config.ToolSightInterval * 3f, _candidates);

            foreach (ToolSighting sighting in sightings)
            {
                if (!string.IsNullOrEmpty(sighting.HolderId) && sighting.HolderId != Id)
                {
                    AILog.Log(AILog.TOOL, Id, $"Knows {sighting.InstanceId} is used by {sighting.HolderId}, searching elsewhere");
                }
            }

            AILog.Log(AILog.TOOL, Id, $"Plan search '{RequiredToolType}' (candidates={_candidates.Count}, budget={budget:F1}s)");
        }

        public bool CanContinue()
        {
            if (!IsActive || HasRequiredTool())
            {
                return false;
            }

            if (Now > _deadline && _targetTool == null)
            {
                return false;
            }

            return _isLookPendingAfterArrival || CandidateIndex + 1 < _candidates.Count || CandidateIndex < 0;
        }

        public bool TrySelectNextTarget(out Vector3 target)
        {
            target = Position;
            CandidateIndex++;

            if (CandidateIndex >= _candidates.Count)
            {
                return false;
            }

            target = _candidates[CandidateIndex];
            _isLookPendingAfterArrival = true;
            AILog.Log(AILog.TOOL, Id, $"Search target {CandidateIndex + 1}/{_candidates.Count} {target}");
            return true;
        }

        public bool TryLookForTool(out Vector3 toolPosition)
        {
            toolPosition = Position;
            bool wasAfterArrival = _isLookPendingAfterArrival;
            _isLookPendingAfterArrival = false;

            _owner.Perception.ScanTools(Position, Config.ToolSightRadius);
            ToolItem found = ToolRegistry.FindAvailableNear(RequiredToolType, Position, Config.ToolSightRadius, Id);

            if (found != null && found.TryReserve(Id))
            {
                ReleaseTarget();
                _targetTool = found;
                toolPosition = found.Position;
                AILog.Log(AILog.TOOL, Id, $"Found {found.InstanceId}, reserved");
                return true;
            }

            if (wasAfterArrival && !_hasRaisedFirstStress)
            {
                _hasRaisedFirstStress = true;
                AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' not at expected place", 1);
            }

            return false;
        }

        public bool TryPickUpTarget()
        {
            ToolItem tool = _targetTool;

            if (tool == null || !tool.IsAvailableFor(Id))
            {
                AILog.Log(AILog.TOOL, Id, "Target tool taken by someone else");
                ReleaseTarget();
                return false;
            }

            _targetTool = null;
            if (!_owner.TryPickUp(tool))
            {
                tool.CancelReservation(Id);
                return false;
            }

            IsActive = false;
            return true;
        }

        public void RaiseFailure()
        {
            AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' failed (checked {Mathf.Max(0, CandidateIndex + 1)}/{_candidates.Count}, {(Now > _deadline ? "time over" : "no more candidates")})");
            AILog.Log(AILog.STUB, Id, $"Inquiry to nearby villagers for '{RequiredToolType}' skipped (interaction branch)");
            AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' search failed", 2);
            AIEvents.RaiseRequest(Id, "FindTool", RequiredToolType);
            Cancel();
            _owner.Core.FailCurrentRoutine($"tool '{RequiredToolType}' not found");
        }

        public void Cancel()
        {
            ReleaseTarget();
            IsActive = false;
        }

        private void ReleaseTarget()
        {
            if (_targetTool != null)
            {
                _targetTool.CancelReservation(Id);
                _targetTool = null;
            }
        }
    }
}
