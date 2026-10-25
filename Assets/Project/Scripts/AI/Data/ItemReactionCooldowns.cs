using System;
using UnityEngine;

namespace ProjectShaman.AI.Data
{
    // How long one receiver ignores the same item after reacting to it, per item (seconds).
    // Every receiver kind (villager, ghost, minion) keeps its own set in its own data, so each is tuned apart
    [Serializable]
    public struct ItemReactionCooldowns
    {
        [Min(0f)] public float Salt;
        [Min(0f)] public float BeanMugwort;
        [Min(0f)] public float Bell;
        [Min(0f)] public float Gong;

        public ItemReactionCooldowns(float salt, float beanMugwort, float bell, float gong)
        {
            Salt = salt;
            BeanMugwort = beanMugwort;
            Bell = bell;
            Gong = gong;
        }

        public float Get(PlayerItemKind item)
        {
            switch (item)
            {
                case PlayerItemKind.Salt: return Mathf.Max(0f, Salt);
                case PlayerItemKind.BeanMugwort: return Mathf.Max(0f, BeanMugwort);
                case PlayerItemKind.Bell: return Mathf.Max(0f, Bell);
                case PlayerItemKind.Gong: return Mathf.Max(0f, Gong);
                default: return 0f;
            }
        }
    }
}
