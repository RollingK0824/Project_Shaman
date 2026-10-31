using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class Animal : MonoBehaviour, IGhostTarget
    {
        [Tooltip("Which body its corpse trace shows (AI_CorpseVisual key): Cattle, Chicken, Pig")]
        [SerializeField] private string _corpseKey;

        private IDamageable _damageable;

        public string CorpseKey => _corpseKey;

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
