using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    // One routine's search, in rounds with no time limit: first the tool's own shed, then the remembered spots; when none has the
    // tool, the villager asks the nearest villager (what it hears goes into its memory) and the next round looks over only
    // what it did not see yet (the new leads). After ToolSearchRounds empty rounds it asks the players (find-tool request)
    public class ToolSearchSession
    {
        private const float SPOT_MERGE_DISTANCE = 2.4f;
        // Roads bend and the walk may be held up: a spot set out for is reached within this many times the straight walk
        private const float SPOT_WALK_ALLOWANCE = 2f;
        // A place's centre is often inside the shed or the stall (off the NavMesh): the spot is the nearest walkable point
        private const float SPOT_SNAP_DISTANCE = 4f;
        // Slack on top of the walk, for turning, opening up and a short hold-up on the way
        private const float SPOT_WALK_MARGIN = 3f;
        private readonly NavMeshPath _path = new NavMeshPath();

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
        private int _round;
        private bool _hasRaisedFirstStress;
        private int _firstStressRoutineSerial = -1;
        private bool _isLookPendingAfterArrival;
        private Vector3 _currentSpot;
        private float _spotArriveBy;
        private int _routineSerial = -1;

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

            // A re-plan within the routine keeps the round, the spots already looked over and the first stress
            if (_routineSerial != routine.Serial)
            {
                _routineSerial = routine.Serial;
                _round = 1;
                _checkedSpots.Clear();
                _checkedPositions.Clear();
            }

            _hasRaisedFirstStress = _firstStressRoutineSerial == routine.Serial;
            _isLookPendingAfterArrival = false;
            CandidateIndex = -1;
            IsActive = true;

            _owner.Perception.ScanTools(Position, Config.ToolSightRadius);

            IReadOnlyList<ToolSighting> sightings = _owner.Memory.ToolSightings.Get(RequiredToolType);
            CollectOpenSightings(sightings);
            ToolSearchPlanner.BuildCandidates(RequiredToolType, _openSightings, Position, SPOT_MERGE_DISTANCE, Now,
                Config.ToolSightInterval * 3f, _checkedPositions, _candidates, _storages);

            foreach (ToolSighting sighting in sightings)
            {
                if (!string.IsNullOrEmpty(sighting.HolderId) && sighting.HolderId != Id)
                {
                    AILog.Log(AILog.TOOL, Id, $"Knows {sighting.InstanceId} is used by {sighting.HolderId}, searching elsewhere");
                }
            }

            AILog.Log(AILog.TOOL, Id, $"Plan search '{RequiredToolType}' round {_round}/{Config.ToolSearchRounds} (candidates={_candidates.Count}, checked={_checkedSpots.Count})");
        }

        // Held in a conversation (a player talking to it) the villager is not walking: the time allowed to reach the spot waits
        public void HoldClock(float seconds)
        {
            if (IsActive && seconds > 0f)
            {
                _spotArriveBy += seconds;
            }
        }

        // Every frame (AI_ToolHandler): a spot that cannot be reached is skipped (the round goes on with the next one),
        // whether or not the behaviour tree looks at CanContinue while walking
        public void TickUnreachableSpot()
        {
            if (IsActive && _isLookPendingAfterArrival && !HasRequiredTool() && !CanContinue())
            {
                RaiseFailure();
            }
        }

        public bool CanContinue()
        {
            if (!IsActive || HasRequiredTool())
            {
                return false;
            }

            // A spot set out for is looked over on arrival, as long as the walk there takes about what it should. One the
            // villager cannot get to (a blocked way) is counted as looked over and the round goes on without it
            if (_isLookPendingAfterArrival)
            {
                if (Now > _spotArriveBy)
                {
                    AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' could not reach spot {CandidateIndex + 1}/{_candidates.Count} {_currentSpot}, skipped");
                    _isLookPendingAfterArrival = false;
                    _checkedPositions.Add(_currentSpot);
                    return false;
                }

                return true;
            }

            // An empty plan still goes on once, so the failure path below ends the round
            if (CandidateIndex + 1 >= _candidates.Count)
            {
                return CandidateIndex < 0;
            }

            return true;
        }

        // The next spot the villager can walk to; one with no path at all is counted as looked over and skipped
        public bool TrySelectNextTarget(out Vector3 target)
        {
            target = Position;

            while (++CandidateIndex < _candidates.Count)
            {
                Vector3 spot = _candidates[CandidateIndex];
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, SPOT_SNAP_DISTANCE, NavMesh.AllAreas))
                {
                    spot = hit.position;
                }

                if (!TryGetWalkLength(spot, out float walk))
                {
                    AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' spot {CandidateIndex + 1}/{_candidates.Count} {_candidates[CandidateIndex]} has no path, skipped");
                    _checkedPositions.Add(_candidates[CandidateIndex]);
                    continue;
                }

                target = spot;
                _currentSpot = _candidates[CandidateIndex];
                _isLookPendingAfterArrival = true;
                _spotArriveBy = Now + walk / Mathf.Max(0.1f, Config.WalkSpeedEstimate) * SPOT_WALK_ALLOWANCE + SPOT_WALK_MARGIN;
                AILog.Log(AILog.TOOL, Id, $"Search target {CandidateIndex + 1}/{_candidates.Count} {target} (walk {walk:F0}m)");
                return true;
            }

            return false;
        }

        // Along the NavMesh, not the straight line: the old allowance gave up on spots behind a long way round
        private bool TryGetWalkLength(Vector3 to, out float length)
        {
            length = 0f;
            Vector3 from = NavMesh.SamplePosition(Position, out NavMeshHit start, SPOT_SNAP_DISTANCE, NavMesh.AllAreas) ? start.position : Position;
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path) || _path.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }

            Vector3[] corners = _path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            // A partial path still counts when it ends close enough to look the spot over
            return corners.Length > 0 && Vector3.Distance(corners[corners.Length - 1], to) <= SPOT_SNAP_DISTANCE;
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

        // Returns false when the search goes on (spots left in this round, or the next round after asking around); the
        // routine stays and the search is planned again afterwards. true: the last round came up empty, the routine fails
        public bool RaiseFailure()
        {
            bool hasSpotsLeft = CandidateIndex + 1 < _candidates.Count;
            string progress = $"checked {Mathf.Clamp(CandidateIndex + 1, 0, _candidates.Count)}/{_candidates.Count}";

            if (hasSpotsLeft)
            {
                Cancel();
                return false;
            }

            if (_round < Config.ToolSearchRounds)
            {
                // Next round: only the leads heard now (a shed named again counts as new). No lead: the next villager is asked
                _round++;
                bool asking = _owner.Inquiry.TryStart(RequiredToolType);
                AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' round {_round - 1} empty ({progress}), {(asking ? "asks around" : "nobody nearby to ask")}, round {_round} next");
                Cancel();
                return false;
            }

            AILog.Log(AILog.TOOL, Id, $"Search '{RequiredToolType}' failed after {_round} rounds ({progress})");

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
