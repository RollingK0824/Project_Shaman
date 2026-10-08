using Mirror;
using UnityEngine;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

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
        [SyncVar] private VillagerGender _syncGender;
        [SyncVar] private VillagerSocialClass _syncSocialClass;
        [SyncVar] private VillagerAgeGroup _syncAgeGroup;

        [SyncVar] private int _syncCycleDays;
        [SyncVar] private int _syncSlotsPerDay;
        private readonly SyncList<RoutineSlotInfo> _syncSchedule = new SyncList<RoutineSlotInfo>();

        private AI_Core aiCore;
        private AI_Movement aiMovement;
        private AI_VisualController visualController;

        void Awake()
        {
            aiCore = GetComponent<AI_Core>();
            aiMovement = GetComponent<AI_Movement>();
            visualController = GetComponent<AI_VisualController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            WritePublicInfoToSyncVars();

            if (aiCore != null)
            {
                aiCore.OnSymptomTriggered += HandleSymptomTriggered;
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
                    gameObject.name = $"Villager_{_syncVillagerId}";
                    RegisterToRegistry();
                }
            }
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
        }

        public override void OnStopServer()
        {
            if (aiCore != null)
            {
                aiCore.OnSymptomTriggered -= HandleSymptomTriggered;
            }

            base.OnStopServer();
        }

        [Server]
        private void HandleSymptomTriggered(SymptomType symptom)
        {
            RpcTriggerEffect($"Symptom{symptom}");
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
            _syncGender = info.Gender;
            _syncSocialClass = info.SocialClass;
            _syncAgeGroup = info.AgeGroup;

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

        private VillagerPublicInfo ReadPublicInfoFromSyncVars()
        {
            return new VillagerPublicInfo
            {
                VillagerId = _syncVillagerId,
                DisplayName = _syncDisplayName,
                JobId = _syncJobId,
                Gender = _syncGender,
                SocialClass = _syncSocialClass,
                AgeGroup = _syncAgeGroup
            };
        }

        [ServerCallback]
        void Update()
        {
            if (aiCore != null && _networkState != aiCore.CurrentState)
            {
                _networkState = aiCore.CurrentState;
            }
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
