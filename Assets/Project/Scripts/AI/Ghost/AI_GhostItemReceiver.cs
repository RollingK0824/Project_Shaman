using UnityEngine;

namespace ProjectShaman.AI.Ghost
{
    // A player's ritual item reaching a night ghost. The ghost has no health (C76): items act on its channel instead.
    // Salt breaks it, bean-mugwort pushes it back, the gong holds it still, the bell shows it to every player.
    [RequireComponent(typeof(AI_GhostEntity))]
    public class AI_GhostItemReceiver : MonoBehaviour, IItemControlReceiver, ISaltReactable
    {
        private AI_GhostEntity _entity;

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
        }

        public bool ReactToSalt(GameObject user)
        {
            float retreat = _entity.Config != null ? _entity.Config.GhostSaltRetreatSeconds : 0f;
            return _entity.InterruptChannel($"salt from {NameOf(user)}", retreat);
        }

        public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
        {
            if (!effect.IsValid)
            {
                return false;
            }

            string from = NameOf(source);

            switch (effect.Kind)
            {
                case ItemControlKind.Slow:
                    float ratio = _entity.Config != null ? _entity.Config.GhostBeanRewindRatio : 0f;
                    return _entity.RewindChannel(ratio, $"bean-mugwort from {from}");

                case ItemControlKind.SensoryInterference:
                    return _entity.PauseChannel(effect.Duration, $"gong from {from}");

                case ItemControlKind.Stagger:
                    if (_entity.Config == null)
                    {
                        return false;
                    }

                    _entity.Reveal(_entity.Config.GhostBellRevealSeconds, $"bell from {from}");
                    return true;

                default:
                    return false;
            }
        }

        private static string NameOf(GameObject user)
        {
            return user != null ? user.name : "unknown";
        }
    }
}
