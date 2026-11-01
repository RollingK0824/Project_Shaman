using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
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

        private const float PUT_AWAY_LINGER_SECONDS = 1f;
        private const int STORAGE_POINT_TRIES = 8;
        private const float STORAGE_TOOL_SPACING = 0.8f;

        private System.Random _storageRandom;
        // How often a searching villager looks round for someone to ask on the way
        private const float ASK_ON_THE_WAY_INTERVAL = 0.5f;
        private float _nextAskOnTheWayTime;

        private ToolItem _heldTool;
        private ToolSearchSession _search;
        private ToolInquiry _inquiry;
        private AI_Interaction _interaction;
        private PlaceArea _putAwayStorage;
        private bool _isPutAwayPending;
        private bool _isPuttingAway;

        public AI_Core Core { get; private set; }
        public AI_Memory Memory { get; private set; }
        public AI_Perception Perception { get; private set; }
        public INetworkMovable Movable { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public AIRuntimeContext Context { get; private set; }

        public ToolSearchSession Search => _search;
        public ToolInquiry Inquiry => _inquiry;
        public ToolItem HeldTool => _heldTool;
        public bool IsHolding => _heldTool != null;
        public string VillagerId => Core.LogId;

        private Transform _foundHand;

        // The right hand of the body in use (the Humanoid bone, else a bone named hand_r), found again after the body is
        // swapped for the villager's gender. The root only when the body has no hand, so a tool never stays behind at the feet
        private Transform Socket
        {
            get
            {
                if (_handSocket != null)
                {
                    return _handSocket;
                }

                if (_foundHand == null)
                {
                    _foundHand = FindRightHand();
                }

                return _foundHand != null ? _foundHand : transform;
            }
        }

        private Transform FindRightHand()
        {
            foreach (Animator animator in GetComponentsInChildren<Animator>())
            {
                if (animator.isHuman)
                {
                    Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (bone != null)
                    {
                        return bone;
                    }
                }
            }

            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child.name == "hand_r")
                {
                    return child;
                }
            }

            return null;
        }

        private void Awake()
        {
            Core = GetComponent<AI_Core>();
            Memory = GetComponent<AI_Memory>();
            Perception = GetComponent<AI_Perception>();
            Movable = GetComponent<INetworkMovable>();
            Agent = GetComponent<NavMeshAgent>();
            _interaction = GetComponent<AI_Interaction>();
            _search = new ToolSearchSession(this);
            _inquiry = new ToolInquiry(this);

            Core.OnRoutineApplied += HandleRoutineApplied;
            Core.OnRoutineCleared += HandleRoutineCleared;

            if (_interaction != null)
            {
                _interaction.OnInteractionEnded += HandleInteractionEnded;
            }
        }

        private void OnDestroy()
        {
            if (Core != null)
            {
                Core.OnRoutineApplied -= HandleRoutineApplied;
                Core.OnRoutineCleared -= HandleRoutineCleared;
            }

            if (_interaction != null)
            {
                _interaction.OnInteractionEnded -= HandleInteractionEnded;
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

        private void Update()
        {
            // The body was swapped while a tool was held: the old hand is gone, the tool follows the new one
            if (_heldTool != null && _heldTool.HolderSocket != Socket)
            {
                _heldTool.RebindSocket(Socket);
            }

            if (Context != null)
            {
                _inquiry.Tick();
                if (_interaction != null && _interaction.IsInteracting && !_inquiry.IsActive)
                {
                    _search.HoldClock(Time.deltaTime);
                }

                if (Time.time >= _nextAskOnTheWayTime)
                {
                    _nextAskOnTheWayTime = Time.time + ASK_ON_THE_WAY_INTERVAL;
                    _search.TickAskOnTheWay();
                }

                _search.TickUnreachableSpot();
                TickPutAway();
            }
        }

        public bool PutAwayUnlessNeeded(string reason)
        {
            if (!IsHolding)
            {
                return false;
            }

            if (ToolTidyPolicy.ShouldKeepForRoutine(_heldTool.TypeId, Core.CurrentRoutine.ToolTypeId))
            {
                AILog.Log(AILog.TOOL, VillagerId, $"Keep {_heldTool.InstanceId} for current routine ({reason})");
                return false;
            }

            if (!PlaceRegistry.TryGet(_heldTool.HomeStoragePlaceId, out PlaceArea storage) || _interaction == null)
            {
                return false;
            }

            _putAwayStorage = storage;
            _isPutAwayPending = true;
            AILog.Log(AILog.TOOL, VillagerId, $"Put away {_heldTool.InstanceId} at {storage.PlaceId} ({reason})");
            return true;
        }

        private void TickPutAway()
        {
            if (!_isPutAwayPending)
            {
                return;
            }

            if (!IsHolding || IsMustGoHome() || ToolTidyPolicy.ShouldKeepForRoutine(_heldTool.TypeId, Core.CurrentRoutine.ToolTypeId))
            {
                ClearPutAway();
                return;
            }

            if (!Core.IsSimulating || _interaction.IsInteracting)
            {
                return;
            }

            if (_interaction.TryBegin(_putAwayStorage, InteractionResponse.Approach, PUT_AWAY_LINGER_SECONDS))
            {
                _isPutAwayPending = false;
                _isPuttingAway = true;
            }
        }

        private void HandleInteractionEnded(InteractionResult result, InteractionEndReason reason)
        {
            if (!_isPuttingAway || _putAwayStorage == null || result.InteractableId != _putAwayStorage.InteractableId)
            {
                return;
            }

            _isPuttingAway = false;

            if (reason == InteractionEndReason.Completed && IsHolding)
            {
                Drop(PickStoragePoint(_putAwayStorage), $"put away at {_putAwayStorage.PlaceId}");
                ClearPutAway();
                return;
            }

            _isPutAwayPending = IsHolding;
        }

        // Somewhere in the storage clear of the tools already there: every villager used to drop at the one approach point,
        // so the tools piled up on a single spot
        private Vector3 PickStoragePoint(PlaceArea storage)
        {
            Vector3 best = transform.position;
            float bestGap = -1f;
            for (int i = 0; i < STORAGE_POINT_TRIES; i++)
            {
                _storageRandom ??= new System.Random(GetInstanceID());
                Vector3 candidate = storage.GetRandomPoint(_storageRandom);
                float gap = float.MaxValue;
                foreach (ToolItem tool in ToolRegistry.All)
                {
                    if (tool == null || tool == _heldTool)
                    {
                        continue;
                    }

                    Vector3 offset = tool.transform.position - candidate;
                    offset.y = 0f;
                    gap = Mathf.Min(gap, offset.magnitude);
                }

                if (gap >= STORAGE_TOOL_SPACING)
                {
                    return candidate;
                }

                if (gap > bestGap)
                {
                    best = candidate;
                    bestGap = gap;
                }
            }

            return best;
        }

        private void ClearPutAway()
        {
            _putAwayStorage = null;
            _isPutAwayPending = false;
            _isPuttingAway = false;
        }

        private bool IsMustGoHome()
        {
            return Core.TryGetBlackboardVariable(AI_Core.BB_MUST_GO_HOME, out bool mustGoHome) && mustGoHome;
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
            _inquiry.Cancel("routine changed");

            if (!IsHolding)
            {
                return;
            }

            if (ToolTidyPolicy.ShouldKeepForRoutine(_heldTool.TypeId, routine.ToolTypeId))
            {
                AILog.Log(AILog.TOOL, VillagerId, $"Keep {_heldTool.InstanceId} for next routine {routine.Source?.RoutineId}");
                return;
            }

            if (!PutAwayUnlessNeeded($"routine changed to {routine.Source?.RoutineId}"))
            {
                Drop(transform.position, $"routine changed to {routine.Source?.RoutineId}");
            }
        }

        private void HandleRoutineCleared(string reason)
        {
            _search.Cancel();
            _inquiry.Cancel($"routine cleared ({reason})");

            if (IsHolding && (IsMustGoHome() || !PutAwayUnlessNeeded($"routine cleared ({reason})")))
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
