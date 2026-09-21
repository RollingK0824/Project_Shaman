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
            animator = GetComponent<Animator>();

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

        public void TriggerActionEffect(string triggerName)
        {
            if (animator != null)
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
