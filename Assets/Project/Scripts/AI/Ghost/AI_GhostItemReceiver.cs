using UnityEngine;

namespace ProjectShaman.AI.Ghost
{
    // A player's ritual item reaching a night ghost, only within that item's own range. The ghost has no health (C76):
    // salt breaks it off and flings it elsewhere (angrier), bean-mugwort slows it, the gong holds its channel still,
    // the bell makes it glow for the players around. Each item has its own reaction cooldown (AIBehaviourConfig)
    [RequireComponent(typeof(AI_GhostEntity))]
    public class AI_GhostItemReceiver : MonoBehaviour, IItemControlReceiver, ISaltReactable
    {
        private readonly ItemReactionGate _gate = new ItemReactionGate();
        private AI_GhostEntity _entity;

        private void Awake()
        {
            _entity = GetComponent<AI_GhostEntity>();
        }

        public bool ReactToSalt(GameObject user)
        {
            if (_entity.Config == null || user == null || !_gate.IsReady(PlayerItemKind.Salt))
            {
                return false;
            }

            return Consume(PlayerItemKind.Salt, _entity.RepelBySalt(user.transform.position, $"salt from {user.name}"));
        }

        public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
        {
            if (_entity.Config == null || !effect.IsValid)
            {
                return false;
            }

            string from = source != null ? source.name : "unknown";

            switch (effect.Kind)
            {
                case ItemControlKind.Slow:
                    return _gate.IsReady(PlayerItemKind.BeanMugwort)
                        && Consume(PlayerItemKind.BeanMugwort, _entity.SlowDown(effect.SpeedMultiplier, effect.Duration, $"bean-mugwort from {from}"));

                case ItemControlKind.SensoryInterference:
                    return _gate.IsReady(PlayerItemKind.Gong)
                        && Consume(PlayerItemKind.Gong, _entity.PauseChannel(effect.Duration, $"gong from {from}"));

                case ItemControlKind.Stagger:
                    if (!_gate.IsReady(PlayerItemKind.Bell))
                    {
                        return false;
                    }

                    _entity.Reveal(_entity.Config.GhostBellRevealSeconds, $"bell from {from}");
                    return Consume(PlayerItemKind.Bell, true);

                default:
                    return false;
            }
        }

        private bool Consume(PlayerItemKind item, bool reacted)
        {
            if (reacted)
            {
                _gate.Consume(item, _entity.Config.GhostItemCooldowns);
            }

            return reacted;
        }
    }
}
