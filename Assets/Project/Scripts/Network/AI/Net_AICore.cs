using System.Collections.Generic;
using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Core), typeof(AI_Movement))]
    public class Net_AICore : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnStateChanged))]
        private AIState _networkState = AIState.Idle;

        [SyncVar] private string _syncVillagerId;
        [SyncVar] private string _syncDisplayName;
        [SyncVar] private string _syncJobId;
        [SyncVar] private string _syncJobName;
        [SyncVar] private VillagerGender _syncGender;
        [SyncVar] private VillagerSocialClass _syncSocialClass;
        [SyncVar] private VillagerAgeGroup _syncAgeGroup;
        [SyncVar] private string _syncHomePlaceId;
        [SyncVar(hook = nameof(OnPossessionMarkChanged))] private bool _syncPossessionMarked;
        // Appearance symptoms active now (bit mask over SymptomType): shadow, skin and eyes come and go on every peer
        [SyncVar(hook = nameof(OnAppearanceMaskChanged))] private int _syncAppearance;
        // Action animation by Animator state name: clients cross-fade it themselves, which NetworkAnimator does not replicate
        [SyncVar(hook = nameof(OnActionAnimationChanged))] private string _syncActionAnimation = string.Empty;

        [SyncVar] private int _syncCycleDays;
        [SyncVar] private int _syncSlotsPerDay;
        private readonly SyncList<RoutineSlotInfo> _syncSchedule = new SyncList<RoutineSlotInfo>();

        // Today's block hours change every morning; the day and home/bed hours are written first so the hours hook sees them
        [SyncVar] private int _syncBlocksDay;
        [SyncVar] private int _syncLeaveHour;
        [SyncVar] private int _syncHomeHour;
        [SyncVar] private int _syncBedHour;
        [SyncVar(hook = nameof(OnBlockHoursChanged))] private int[] _syncBlockHours;

        private AI_Core aiCore;
        private AI_Movement aiMovement;
        private AI_VisualController visualController;

        void Awake()
        {
            aiCore = GetComponent<AI_Core>();
            aiMovement = GetComponent<AI_Movement>();
            visualController = GetComponent<AI_VisualController>();
            if (TryGetComponent(out AI_VillagerModel model))
            {
                model.OnModelChanged += HandleModelChanged;
            }
        }

        // Parameters (Speed) are replicated from and onto the current body's Animator
        private void HandleModelChanged(Animator animator)
        {
            if (TryGetComponent(out NetworkAnimator networkAnimator))
            {
                networkAnimator.animator = animator;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            WritePublicInfoToSyncVars();

            if (aiCore != null)
            {
                aiCore.OnSymptomTriggered += HandleSymptomTriggered;
                aiCore.OnAppearanceChanged += WriteAppearance;
                aiCore.OnEffectTriggered += RpcTriggerEffect;
                aiCore.OnTodayBlocksChanged += WriteTodayBlocks;
                WriteTodayBlocks();
            }

            if (aiMovement != null) aiMovement.SetAgentActive(true);
            if (aiCore != null) aiCore.SetBehaviorGraphActive(true);
            if (visualController != null) visualController.UpdateAnimatorLocally = true;

            AILog.Log(AILog.NET, aiCore != null ? aiCore.LogId : name, "OnStartServer → Agent/BT enabled");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            AILog.Log(AILog.NET, string.IsNullOrEmpty(_syncVillagerId) ? name : _syncVillagerId, $"OnStartClient (isServer={isServer}, netId={netId}, pos={transform.position})");

            if (!isServer)
            {
                if (aiMovement != null) aiMovement.SetAgentActive(false);
                if (aiCore != null) aiCore.SetBehaviorGraphActive(false);
                if (visualController != null) visualController.UpdateAnimatorLocally = false;

                if (aiCore != null)
                {
                    aiCore.ApplyNetworkState(_networkState);
                    aiCore.ApplyNetworkSchedule(VillagerScheduleView.FromFlat(_syncCycleDays, _syncSlotsPerDay, _syncSchedule));
                    aiCore.ApplyNetworkPublicInfo(ReadPublicInfoFromSyncVars());
                    aiCore.SetTodayBlocks(_syncBlocksDay, _syncBlockHours, _syncLeaveHour, _syncHomeHour, _syncBedHour);
                    ApplyPossessionMark(_syncPossessionMarked);
                    ApplyActionAnimation(_syncActionAnimation);
                    aiCore.ApplyNetworkAppearance(_syncAppearance);
                    gameObject.name = $"Villager_{_syncVillagerId}";
                    RegisterToRegistry();
                }
            }

            SpeechBubbleOverlay.ShowName(transform, _syncDisplayName);
        }

        public override void OnStopClient()
        {
            SpeechBubbleOverlay.HideName(transform);
            base.OnStopClient();
        }

        private void RegisterToRegistry()
        {
            VillagerRegistry registry = AIManager.Instance != null ? AIManager.Instance.Registry : null;
            if (registry == null)
            {
                AILog.Warn(AILog.NET, $"{aiCore.LogId} no AIManager/VillagerRegistry in scene, not registered");
                return;
            }

            registry.Register(aiCore.PublicInfo, aiCore.ScheduleView);

            // Same house, same owner on every client: the scene places are identical, only the assignment is told
            if (!string.IsNullOrEmpty(aiCore.PublicInfo.HomePlaceId) && PlaceRegistry.TryGet(aiCore.PublicInfo.HomePlaceId, out PlaceArea home) && string.IsNullOrEmpty(home.OwnerId))
            {
                home.SetOwner(aiCore.PublicInfo.VillagerId);
            }
        }

        public override void OnStopServer()
        {
            if (aiCore != null)
            {
                aiCore.OnSymptomTriggered -= HandleSymptomTriggered;
                aiCore.OnAppearanceChanged -= WriteAppearance;
                aiCore.OnEffectTriggered -= RpcTriggerEffect;
                aiCore.OnTodayBlocksChanged -= WriteTodayBlocks;
            }

            base.OnStopServer();
        }

        [Server]
        private void HandleSymptomTriggered(SymptomType symptom)
        {
            RpcTriggerEffect($"Symptom{symptom}");
        }

        [Server]
        private void WriteAppearance(int mask)
        {
            _syncAppearance = mask;
        }

        private void OnAppearanceMaskChanged(int oldValue, int newValue)
        {
            // The server's AI_Core already holds it
            if (!isServer && aiCore != null)
            {
                aiCore.ApplyNetworkAppearance(newValue);
            }
        }

        [Server]
        private void WritePublicInfoToSyncVars()
        {
            if (aiCore == null || aiCore.PublicInfo == null)
            {
                AILog.Warn(AILog.NET, $"{name} spawned without public info");
                return;
            }

            VillagerPublicInfo info = aiCore.PublicInfo;
            _syncVillagerId = info.VillagerId;
            _syncDisplayName = info.DisplayName;
            _syncJobId = info.JobId;
            _syncJobName = info.JobName;
            _syncGender = info.Gender;
            _syncSocialClass = info.SocialClass;
            _syncAgeGroup = info.AgeGroup;
            _syncHomePlaceId = info.HomePlaceId;

            AILog.Log(AILog.NET, info.VillagerId, "Public info written to SyncVars");
            WriteScheduleToSyncList();
        }

        [Server]
        private void WriteScheduleToSyncList()
        {
            VillagerScheduleView schedule = aiCore.ScheduleView;
            _syncSchedule.Clear();

            if (schedule == null)
            {
                AILog.Warn(AILog.NET, $"{aiCore.LogId} spawned without schedule");
                return;
            }

            _syncCycleDays = schedule.CycleDays;
            _syncSlotsPerDay = schedule.SlotsPerDay;
            _syncSchedule.AddRange(schedule.FlatSlots);

            AILog.Log(AILog.NET, aiCore.LogId, $"Schedule written to SyncList ({schedule.CycleDays}x{schedule.SlotsPerDay})");
        }

        [Server]
        private void WriteTodayBlocks()
        {
            _syncBlocksDay = aiCore.TodayBlocksDay;
            _syncLeaveHour = aiCore.TodayLeaveHour;
            _syncHomeHour = aiCore.TodayHomeHour;
            _syncBedHour = aiCore.TodayBedHour;
            _syncBlockHours = new List<int>(aiCore.TodayBlockHours).ToArray();
        }

        private void OnBlockHoursChanged(int[] oldValue, int[] newValue)
        {
            if (!isServer && aiCore != null)
            {
                aiCore.SetTodayBlocks(_syncBlocksDay, newValue, _syncLeaveHour, _syncHomeHour, _syncBedHour);
            }
        }

        private VillagerPublicInfo ReadPublicInfoFromSyncVars()
        {
            return new VillagerPublicInfo
            {
                VillagerId = _syncVillagerId,
                DisplayName = _syncDisplayName,
                JobId = _syncJobId,
                JobName = _syncJobName,
                Gender = _syncGender,
                SocialClass = _syncSocialClass,
                AgeGroup = _syncAgeGroup,
                HomePlaceId = _syncHomePlaceId
            };
        }

        [ServerCallback]
        void Update()
        {
            if (aiCore != null && _networkState != aiCore.CurrentState)
            {
                _networkState = aiCore.CurrentState;
            }

            if (visualController != null && _syncActionAnimation != visualController.CurrentAction)
            {
                _syncActionAnimation = visualController.CurrentAction;
            }

            AIBehaviourConfig config = AIManager.Instance != null ? AIManager.Instance.Config : null;
            bool marked = config != null && config.ShowPossessedMaterial && TryGetComponent(out AI_Possession _);
            if (marked != _syncPossessionMarked)
            {
                _syncPossessionMarked = marked;
                ApplyPossessionMark(marked);
            }
        }

        private void OnPossessionMarkChanged(bool oldValue, bool newValue)
        {
            ApplyPossessionMark(newValue);
        }

        private void OnActionAnimationChanged(string oldValue, string newValue)
        {
            // The server already played it when the work began
            if (!isServer)
            {
                ApplyActionAnimation(newValue);
            }
        }

        private void ApplyActionAnimation(string state)
        {
            if (visualController == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(state))
            {
                visualController.StopAction();
            }
            else
            {
                visualController.PlayAction(state);
            }
        }

        private void ApplyPossessionMark(bool marked)
        {
            if (!TryGetComponent(out AI_PossessionMarker marker))
            {
                if (!marked)
                {
                    return;
                }

                marker = gameObject.AddComponent<AI_PossessionMarker>();
            }

            marker.SetMarked(marked);
        }

        private void OnStateChanged(AIState oldVal, AIState newVal)
        {
            if (!isServer && aiCore != null)
            {
                aiCore.ApplyNetworkState(newVal);
            }
        }

        [ClientRpc]
        public void RpcTriggerEffect(string triggerName)
        {
            if (visualController != null)
            {
                visualController.TriggerActionEffect(triggerName);
            }
        }
    }
}
