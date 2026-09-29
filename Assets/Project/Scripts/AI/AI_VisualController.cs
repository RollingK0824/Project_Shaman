using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Core), typeof(AI_Movement))]
    public class AI_VisualController : MonoBehaviour
    {
        private AI_Core core;
        private AI_Movement movement;
        private Animator animator;

        public bool UpdateAnimatorLocally { get; set; } = true;

        void Awake()
        {
            core = GetComponent<AI_Core>();
            movement = GetComponent<AI_Movement>();
            animator = GetComponentInChildren<Animator>();

            core.OnStateChanged += HandleStateChanged;
        }

        void Update()
        {
            if (UpdateAnimatorLocally && animator != null && movement != null)
            {
                animator.SetFloat("MoveSpeed", movement.CurrentSpeed);
            }
        }

        private void HandleStateChanged(AIState oldState, AIState newState)
        {
            if (animator != null)
            {
                animator.SetInteger("State", (int)newState);
            }
        }

        public Animator Animator => animator;

        public bool HasParameter(string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetIntegerIfExists(string parameterName, int value)
        {
            if (HasParameter(parameterName))
            {
                animator.SetInteger(parameterName, value);
            }
        }

        public void TriggerActionEffect(string triggerName)
        {
            if (HasParameter(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        void OnDestroy()
        {
            if (core != null)
            {
                core.OnStateChanged -= HandleStateChanged;
            }
        }
    }
}
