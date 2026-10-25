using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI
{
    // One receiver's per-item reaction cooldown: after reacting to an item it ignores that same item for a while.
    // Other items still get through, each on its own timer
    public class ItemReactionGate
    {
        private readonly Dictionary<PlayerItemKind, float> _readyAt = new Dictionary<PlayerItemKind, float>();

        public bool IsReady(PlayerItemKind item)
        {
            return !_readyAt.TryGetValue(item, out float readyAt) || Time.time >= readyAt;
        }

        // Call once the reaction really happened
        public void Consume(PlayerItemKind item, ItemReactionCooldowns cooldowns)
        {
            _readyAt[item] = Time.time + cooldowns.Get(item);
        }

        public void Clear()
        {
            _readyAt.Clear();
        }
    }
}
