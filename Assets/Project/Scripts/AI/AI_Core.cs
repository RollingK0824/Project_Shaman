using System;
using UnityEngine;
using Unity.Behavior;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_Core : MonoBehaviour
    {
        public const string BB_CURRENT_STATE = "CurrentState";
        public const string BB_HOME_POSITION = "HomePosition";
        public const string BB_WORK_POSITION = "WorkPosition";
        public const string BB_MUST_GO_HOME = "MustGoHome";
        public const string BB_ROUTINE_SERIAL = "RoutineSerial";
        public const string BB_ROUTINE_CATEGORY = "RoutineCategory";

        [SerializeField] private AIState currentState = AIState.Idle;

        private BehaviorGraphAgent btAgent;
        private VillagerProfile _profile;
        [SerializeField] private VillagerPublicInfo _publicInfo;
        [SerializeField] private RoutineDebugView _routineDebug = new RoutineDebugView();

        public AIState CurrentState => currentState;
        public VillagerProfile Profile => _profile;
        public VillagerPublicInfo PublicInfo => _publicInfo;
        public bool IsInitialized => _profile != null;
        public ResolvedRoutine CurrentRoutine { get; private set; }
        public bool HasRoutine => CurrentRoutine.Source != null;
        public string LogId => _publicInfo != null ? _publicInfo.VillagerId : name;

        public event Action<AIState, AIState> OnStateChanged;
        public event Action<VillagerPublicInfo> OnPublicInfoApplied;

        void Awake()
        {
            btAgent = GetComponent<BehaviorGraphAgent>();
        }

        public void Initialize(VillagerProfile profile)
        {
            if (profile == null)
            {
                AILog.Error(AILog.CORE, $"{name} Initialize with null profile");
                return;
            }

            _profile = profile;
            _publicInfo = profile.PublicInfo;

            _routineDebug.SetSchedule(profile.Schedule);
            AILog.Log(AILog.CORE, LogId, $"Initialize {_publicInfo}");
            OnPublicInfoApplied?.Invoke(_publicInfo);
        }

        public void ApplyNetworkPublicInfo(VillagerPublicInfo publicInfo)
        {
            _publicInfo = publicInfo;
            AILog.Log(AILog.NET, LogId, $"Public info applied on client {_publicInfo}");
            OnPublicInfoApplied?.Invoke(_publicInfo);
        }

        public void SetState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable(BB_CURRENT_STATE, currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void ApplyNetworkState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable(BB_CURRENT_STATE, currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void SetBehaviorGraphActive(bool active)
        {
            if (btAgent == null)
            {
                return;
            }

            if (active)
            {
                ApplyProfileToBlackboard();
            }

            btAgent.enabled = active;
            AILog.Log(AILog.CORE, LogId, $"BehaviorGraph {(active ? "enabled" : "disabled")}");
        }

        private void ApplyProfileToBlackboard()
        {
            if (_profile == null)
            {
                AILog.Warn(AILog.CORE, $"{name} BT enabled without profile, Blackboard not injected");
                return;
            }

            SetBlackboardVariable(BB_HOME_POSITION, _profile.HomePosition);
            SetBlackboardVariable(BB_MUST_GO_HOME, false);
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, RoutineCategory.None);
            AILog.Log(AILog.CORE, LogId, $"Blackboard injected Home={_profile.HomePosition}");
        }

        public void ApplyRoutine(ResolvedRoutine resolved)
        {
            CurrentRoutine = resolved;
            SetBlackboardVariable(BB_WORK_POSITION, resolved.TargetPosition);
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, resolved.Category);
            SetBlackboardVariable(BB_ROUTINE_SERIAL, resolved.Serial);
            _routineDebug.SetCurrent(resolved);
            AILog.Log(AILog.CORE, LogId, $"Blackboard routine {resolved}");
        }

        public void ClearRoutine(int serial, string reason)
        {
            CurrentRoutine = default;
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, RoutineCategory.None);
            SetBlackboardVariable(BB_ROUTINE_SERIAL, serial);
            _routineDebug.ClearCurrent();
            _routineDebug.Serial = serial;
            AILog.Log(AILog.CORE, LogId, $"Blackboard routine cleared #{serial} ({reason})");
        }

        public void SetDailyStartOffset(float offsetSeconds)
        {
            _routineDebug.StartOffsetSeconds = offsetSeconds;
        }

        public void SetMustGoHome(bool mustGoHome)
        {
            SetBlackboardVariable(BB_MUST_GO_HOME, mustGoHome);
            _routineDebug.MustGoHome = mustGoHome;
            AILog.Log(AILog.CORE, LogId, $"Blackboard MustGoHome={mustGoHome}");
        }

        public void SetBlackboardVariable<T>(string variableName, T value)
        {
            if (btAgent != null && btAgent.BlackboardReference != null)
            {
                btAgent.BlackboardReference.SetVariableValue(variableName, value);
            }
        }

        public bool TryGetBlackboardVariable<T>(string variableName, out T value)
        {
            if (btAgent != null && btAgent.BlackboardReference != null)
            {
                return btAgent.BlackboardReference.GetVariableValue(variableName, out value);
            }

            value = default;
            return false;
        }
    }
}
