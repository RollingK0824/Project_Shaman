using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class Animal : MonoBehaviour, IGhostTarget
    {
        private IDamageable _damageable;

        public GhostTargetKind Kind => GhostTargetKind.Animal;
        public bool IsDead => _damageable != null && _damageable.IsDead;

        private void Awake()
        {
            _damageable = GetComponentInParent<IDamageable>();
        }

        private void OnEnable()
        {
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            GhostTargetRegistry.Unregister(this);
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            return context.Verb == GhostActionVerb.Attack && _damageable != null && !_damageable.IsDead;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }
    }
}
