using UnityEngine;

namespace ProjectShaman.AI.World
{
    [RequireComponent(typeof(WorkStation))]
    public class LocalWorkStationReaction : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _hitTrigger = "Hit";

        private WorkStation _station;

        private void Awake()
        {
            _station = GetComponent<WorkStation>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            _station.OnWorkAccepted += HandleWorkAccepted;
        }

        private void OnDisable()
        {
            _station.OnWorkAccepted -= HandleWorkAccepted;
        }

        private void HandleWorkAccepted(string actorId, string actionId)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_hitTrigger);
            }
        }
    }
}
