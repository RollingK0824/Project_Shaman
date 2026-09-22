using System;
using UnityEngine;
using Unity.Behavior;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_Core : MonoBehaviour
    {
        [SerializeField] private AIState currentState = AIState.Idle;

        private BehaviorGraphAgent btAgent;

        public AIState CurrentState => currentState;
        public event Action<AIState, AIState> OnStateChanged;

        void Awake()
        {
            btAgent = GetComponent<BehaviorGraphAgent>();
        }

        public void SetState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable("CurrentState", currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void ApplyNetworkState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable("CurrentState", currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void SetBehaviorGraphActive(bool active)
        {
            if (btAgent != null)
            {
                btAgent.enabled = active;
            }
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
