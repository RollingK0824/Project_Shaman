using UnityEngine;

namespace ProjectShaman.AI.Work
{
    public class AnimationEventRelay : MonoBehaviour
    {
        private AI_WorkPerformer _performer;

        private void Awake()
        {
            _performer = GetComponentInParent<AI_WorkPerformer>();
        }

        public void OnWorkImpact()
        {
            if (_performer != null)
            {
                _performer.HandleImpactEvent();
            }
        }
    }
}
