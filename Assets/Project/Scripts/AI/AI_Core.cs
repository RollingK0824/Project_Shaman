using System;
using UnityEngine;
using Unity.Behavior;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_Core : MonoBehaviour
    {
        public const string BB_CURRENT_STATE = "CurrentState";
        public const string BB_HOME_POSITION = "HomePosition";
        public const string BB_WORK_POSITION = "WorkPosition";

        [SerializeField] private AIState currentState = AIState.Idle;

        private BehaviorGraphAgent btAgent;
        private VillagerProfile _profile;
        [SerializeField] private VillagerPublicInfo _publicInfo;

        public AIState CurrentState => currentState;
        public VillagerProfile Profile => _profile;
        public VillagerPublicInfo PublicInfo => _publicInfo;
        public bool IsInitialized => _profile != null;
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
            SetBlackboardVariable(BB_WORK_POSITION, _profile.InitialWorkPosition);
            AILog.Log(AILog.CORE, LogId, $"Blackboard injected Home={_profile.HomePosition} Work={_profile.InitialWorkPosition}");
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
