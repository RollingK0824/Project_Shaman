using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    // One routine's search: spots are looked over in plan order; once a shed came up empty, each next step goes to
    // whichever is nearer, the next spot or a villager not yet asked (the inquiry re-plans the search afterwards).
    // The time budget starts at the first spot reached, so the walk from home does not eat into it (C109)
    public class ToolSearchSession
    {
        private const float SPOT_MERGE_DISTANCE = 2.4f;

        private struct CheckedSpot
        {
            public Vector3 Position;
            public float Time;
        }

        private readonly AI_ToolHandler _owner;
        private readonly List<Vector3> _candidates = new List<Vector3>();
        private readonly List<Vector3> _storages = new List<Vector3>();
        private readonly List<CheckedSpot> _checkedSpots = new List<CheckedSpot>();
        private readonly List<Vector3> _checkedPositions = new List<Vector3>();
        private readonly List<ToolSighting> _openSightings = new List<ToolSighting>();

        private ToolItem _targetTool;
        private float _budget;
        private float _deadline = float.MaxValue;
        private bool _hasRaisedFirstStress;
        private int _firstStressRoutineSerial = -1;
        private bool _isLookPendingAfterArrival;
        private Vector3 _currentSpot;
        private int _routineSerial = -1;
        private bool _hasCheckedStorage;
        private bool _isAskSkippedUntilNextSpot;

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

            // A re-plan after an inquiry keeps the clock, the spots already looked over and the first stress of this routine
            if (_routineSerial != routine.Serial)
            {
                _routineSerial = routine.Serial;
                _budget = Mathf.Max(1f, routine.EndTime - routine.StartTime) * Config.ToolSearchTimeRatio;
                _deadline = float.MaxValue;
                _checkedSpots.Clear();
                _checkedPositions.Clear();
                _hasCheckedStorage = false;
                _isAskSkippedUntilNextSpot = false;
            }

            _hasRaisedFirstStress = _firstStressRoutineSerial == routine.Serial;
            _isLookPendingAfterArrival = false;
            CandidateIndex = -1;
            IsActive = true;

            _owner.Perception.ScanTools(Position, Config.ToolSightRadius);

            string placeId = routine.Source != null ? routine.PlaceId : null;
            IReadOnlyList<ToolSighting> sightings = _owner.Memory.ToolSightings.Get(RequiredToolType);
            CollectOpenSightings(sightings);
            ToolSearchPlanner.BuildCandidates(RequiredToolType, _openSightings, placeId, _owner.Context.ToolUsePlaces, Position, SPOT_MERGE_DISTANCE, Now,
                Config.ToolSightInterval * 3f, _checkedPositions, _candidates, _storages);

            foreach (ToolSighting sighting in sightings)
            {
                if (!string.IsNullOrEmpty(sighting.HolderId) && sighting.HolderId != Id)
                {
                    AILog.Log(AILog.TOOL, Id, $"Knows {sighting.InstanceId} is used by {sighting.HolderId}, searching elsewhere");
                }
            }

            string clock = _deadline == float.MaxValue ? $"budget={_budget:F1}s from first spot" : $"left={Mathf.Max(0f, _deadline - Now):F1}s";
            AILog.Log(AILog.TOOL, Id, $"Plan search '{RequiredToolType}' (candidates={_candidates.Count}, checked={_checkedSpots.Count}, {clock})");
        }

        public bool CanContinue()
        {
            if (!IsActive || HasRequiredTool())
            {
                return false;
            }

            // A spot set out for is always looked over on arrival, even when the time ran out on the way
            if (_isLookPendingAfterArrival)
            {
                return true;
            }

            if (Now > _deadline && _targetTool == null)
            {
                return false;
            }

            // An empty plan still goes on once, so the failure path below asks around
            if (CandidateIndex + 1 >= _candidates.Count)
            {
                return CandidateIndex < 0;
            }

            return !ShouldAskBefore(_candidates[CandidateIndex + 1]);
        }

        // After an empty shed: a villager nearer than the next spot is asked first (RaiseFailure starts the inquiry).
        // A spot someone told about or that was seen with the tool is gone to straight away
        private bool ShouldAskBefore(Vector3 nextSpot)
        {
            if (!_hasCheckedStorage || _isAskSkippedUntilNextSpot || IsSightingSpot(nextSpot)
                || !_owner.Inquiry.TryFindPartner(RequiredToolType, out float partnerDistance))
            {
                return false;
            }

            return partnerDistance < Vector3.Distance(Position, nextSpot);
        }

        private bool IsSightingSpot(Vector3 spot)
        {
            foreach (ToolSighting sighting in _openSightings)
            {
                if (Vector3.Distance(sighting.Position, spot) < SPOT_MERGE_DISTANCE)
                {
                    return true;
                }
            }

            return false;
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
            _currentSpot = target;
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

            if (wasAfterArrival)
            {
                MarkSpotChecked();
            }

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
                _firstStressRoutineSerial = _owner.Core.CurrentRoutine.Serial;
                AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' not at expected place", Config.ToolMissingStress);
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
            AIEvents.RaiseToolRecovered(Id, tool.TypeId);
            return true;
        }

        // Returns false when the search goes on (an inquiry started, or spots are left); the routine stays and the
        // search is planned again afterwards. Asking around is part of the search, so it stops once the time is over
        public bool RaiseFailure()
        {
            bool isTimeOver = Now > _deadline;
            bool hasSpotsLeft = CandidateIndex + 1 < _candidates.Count;
            string progress = $"checked {Mathf.Clamp(CandidateIndex + 1, 0, _candidates.Count)}/{_candidates.Count}";

            if (!isTimeOver)
            {
                if (_owner.Inquiry.TryStart(RequiredToolType))
                {
                    AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' asks around ({progress}, {(hasSpotsLeft ? "a villager is nearer than the next spot" : "no more spots")})");
                    Cancel();
                    return false;
                }

                if (hasSpotsLeft)
                {
                    _isAskSkippedUntilNextSpot = true;
                    Cancel();
                    return false;
                }
            }

            AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' failed ({progress}, {(isTimeOver ? "time over" : "no more candidates")})");

            _owner.Memory.RecordToolLost(RequiredToolType);
            // Request first: the stress below may leave Calm and ask for a gut, and a giver holds one request at a time
            AIEvents.RaiseRequest(Id, "FindTool", RequiredToolType);
            AIEvents.RaiseStress(Id, $"tool '{RequiredToolType}' search failed", Config.ToolSearchFailedStress);
            Cancel();
            _owner.Core.FailCurrentRoutine($"tool '{RequiredToolType}' not found");
            return true;
        }

        private void MarkSpotChecked()
        {
            _checkedSpots.Add(new CheckedSpot { Position = _currentSpot, Time = Now });
            _checkedPositions.Add(_currentSpot);
            _isAskSkippedUntilNextSpot = false;

            if (ToolSearchPlanner.IsNear(_storages, _currentSpot, SPOT_MERGE_DISTANCE))
            {
                _hasCheckedStorage = true;
            }

            if (_deadline == float.MaxValue)
            {
                _deadline = Now + _budget;
                AILog.Log(AILog.TOOL, Id, $"Search clock starts at first spot ({_budget:F1}s)");
            }
        }

        // Sightings at a spot already looked over are stale unless seen there again since
        private void CollectOpenSightings(IReadOnlyList<ToolSighting> sightings)
        {
            _openSightings.Clear();

            foreach (ToolSighting sighting in sightings)
            {
                if (!IsCheckedSince(sighting.Position, sighting.SeenTime))
                {
                    _openSightings.Add(sighting);
                }
            }
        }

        private bool IsCheckedSince(Vector3 position, float seenTime)
        {
            foreach (CheckedSpot spot in _checkedSpots)
            {
                if (spot.Time >= seenTime && Vector3.Distance(spot.Position, position) < SPOT_MERGE_DISTANCE)
                {
                    return true;
                }
            }

            return false;
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
