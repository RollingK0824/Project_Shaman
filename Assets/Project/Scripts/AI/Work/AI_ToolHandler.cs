using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Perception;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Movement))]
    [RequireComponent(typeof(AI_Memory), typeof(AI_Perception))]
    public class AI_ToolHandler : MonoBehaviour, IAIConfigurable
    {
        private const float DROP_SIDE_OFFSET = 0.7f;

        [SerializeField] private Transform _handSocket;

        [Header("Runtime")]
        [SerializeField] private string _heldToolId;
        [SerializeField] private string _acquireState;
        [SerializeField] private string _requiredToolType;
        [SerializeField] private string _candidateProgress;
        [SerializeField] private ToolTidyZone _tidyZone;
        [SerializeField] private bool _isMovingToStorage;

        private ToolItem _heldTool;
        private ToolSearchSession _search;

        public AI_Core Core { get; private set; }
        public AI_Memory Memory { get; private set; }
        public AI_Perception Perception { get; private set; }
        public INetworkMovable Movable { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public AIRuntimeContext Context { get; private set; }

        public ToolSearchSession Search => _search;
        public ToolItem HeldTool => _heldTool;
        public bool IsHolding => _heldTool != null;
        public string VillagerId => Core.LogId;

        private Transform Socket => _handSocket != null ? _handSocket : transform;

        private void Awake()
        {
            Core = GetComponent<AI_Core>();
            Memory = GetComponent<AI_Memory>();
            Perception = GetComponent<AI_Perception>();
            Movable = GetComponent<INetworkMovable>();
            Agent = GetComponent<NavMeshAgent>();
            _search = new ToolSearchSession(this);

            Core.OnRoutineApplied += HandleRoutineApplied;
            Core.OnRoutineCleared += HandleRoutineCleared;
        }

        private void OnDestroy()
        {
            if (Core != null)
            {
                Core.OnRoutineApplied -= HandleRoutineApplied;
                Core.OnRoutineCleared -= HandleRoutineCleared;
            }

            if (_search != null)
            {
                _search.Cancel();
            }

            if (_heldTool != null)
            {
                _heldTool.Drop(VillagerId, transform.position, "holder destroyed");
                _heldTool = null;
            }
        }

        public void Configure(AIRuntimeContext context)
        {
            Context = context;
        }

        public bool IsHoldingType(string typeId)
        {
            return _heldTool != null && _heldTool.TypeId == typeId;
        }

        public bool TryPickUp(ToolItem tool)
        {
            if (tool == null || _heldTool != null || !tool.TryPickUp(VillagerId, Socket))
            {
                return false;
            }

            _heldTool = tool;
            _heldToolId = tool.InstanceId;
            Memory.RecordToolSighting(tool);
            return true;
        }

        public void Drop(Vector3 position, string reason)
        {
            if (_heldTool == null)
            {
                return;
            }

            ToolItem tool = _heldTool;
            _heldTool = null;
            _heldToolId = string.Empty;
            tool.Drop(VillagerId, position + transform.right * DROP_SIDE_OFFSET, reason);
            Memory.RecordToolSighting(tool);
        }

        public void SetTidyZone(ToolTidyZone zone)
        {
            _tidyZone = zone;
        }

        public ToolTaskStatus BeginTidy()
        {
            _isMovingToStorage = false;
            PlaceArea storage = null;
            bool hasStorage = _heldTool != null && PlaceRegistry.TryGet(_heldTool.HomeStoragePlaceId, out storage);

            switch (ToolTidyPolicy.DecideTidyAction(IsHolding, _tidyZone, hasStorage))
            {
                case ToolTidyAction.GoToStorage:
                    _isMovingToStorage = true;
                    Movable.MoveTo(storage.GetRandomPoint(new System.Random(GetInstanceID())));
                    AILog.Log(AILog.TOOL, VillagerId, $"Tidy {_tidyZone} → storage {storage.PlaceId}");
                    return ToolTaskStatus.Running;
                case ToolTidyAction.DropHere:
                    Drop(transform.position, $"tidy {_tidyZone}");
                    return ToolTaskStatus.Success;
                case ToolTidyAction.CarryHome:
                    AILog.Log(AILog.TOOL, VillagerId, $"Tidy {_tidyZone} → carry {_heldTool.InstanceId} home");
                    return ToolTaskStatus.Success;
                default:
                    return ToolTaskStatus.Success;
            }
        }

        public ToolTaskStatus TickTidy()
        {
            if (!_isMovingToStorage || !IsHolding)
            {
                return ToolTaskStatus.Success;
            }

            if (!Movable.HasReachedDestination)
            {
                return ToolTaskStatus.Running;
            }

            Drop(transform.position, $"tidy {_tidyZone} (storage)");
            _isMovingToStorage = false;
            return ToolTaskStatus.Success;
        }

        public void CancelTidy()
        {
            _isMovingToStorage = false;
        }

        public void DropCarriedAtHome(Vector3 homePosition)
        {
            if (IsHolding)
            {
                Drop(homePosition + transform.forward * 0.8f, "carried home");
            }
        }

        private void HandleRoutineApplied(ResolvedRoutine routine)
        {
            _search.Cancel();

            if (!IsHolding)
            {
                return;
            }

            if (ToolTidyPolicy.ShouldKeepForRoutine(_heldTool.TypeId, routine.ToolTypeId))
            {
                AILog.Log(AILog.TOOL, VillagerId, $"Keep {_heldTool.InstanceId} for next routine {routine.Source?.RoutineId}");
                return;
            }

            Drop(transform.position, $"routine changed to {routine.Source?.RoutineId}");
        }

        private void HandleRoutineCleared(string reason)
        {
            _search.Cancel();

            if (IsHolding)
            {
                Drop(transform.position, $"routine cleared ({reason})");
            }
        }

        public void RefreshSearchDebug()
        {
            _acquireState = _search.IsActive ? "Searching" : "Idle";
            _requiredToolType = _search.RequiredToolType;
            _candidateProgress = $"{Mathf.Max(0, _search.CandidateIndex + 1)}/{_search.CandidateCount}";
        }
    }
}
