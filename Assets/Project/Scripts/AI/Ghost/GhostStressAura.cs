using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    // Being near a ghost wears on a player: every player within the ghost's presence radius gains stress per second,
    // whatever the ghost is doing. A stalked player is always inside it (the radius never drops below the stalk distance),
    // so stalking still feels like it raises stress, while walking up to cut a channel short now costs too.
    // The radius grows with the ghost's yin, pending yin included (salt angers it at once)
    public class GhostStressAura
    {
        private const float TICK_INTERVAL = 0.5f;

        private readonly AI_GhostEntity _owner;
        private readonly AIBehaviourConfig _config;
        private readonly AI_Possession _possession;
        private readonly List<IGhostTarget> _players = new List<IGhostTarget>();
        private readonly HashSet<PlayerStressController> _affected = new HashSet<PlayerStressController>();
        private readonly HashSet<PlayerStressController> _inside = new HashSet<PlayerStressController>();
        private float _nextTickTime;

        public GhostStressAura(AI_GhostEntity owner, AIBehaviourConfig config, AI_Possession possession)
        {
            _owner = owner;
            _config = config;
            _possession = possession;
        }

        public float Radius
        {
            get
            {
                float yin = _possession != null ? _possession.Yin + _possession.PendingYin : 0f;
                float t = _config.MaxYin > 0f ? Mathf.Clamp01(yin / _config.MaxYin) : 0f;
                return Mathf.Max(_config.GhostStalkMaxDistance, Mathf.Lerp(_config.GhostStressRadiusMin, _config.GhostStressRadiusMax, t));
            }
        }

        public int AffectedCount => _affected.Count;

        public void Tick()
        {
            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + TICK_INTERVAL;

            float radius = Radius;
            float rate = _config.GhostStalkStressPerSecond;
            Vector3 from = _owner.transform.position;
            from.y = 0f;
            _inside.Clear();

            GhostTargetRegistry.CollectOfKind(GhostTargetKind.Player, _players);
            foreach (IGhostTarget player in _players)
            {
                if (player == null || (player is Object unityObject && unityObject == null))
                {
                    continue;
                }

                Vector3 position = player.gameObject.transform.position;
                position.y = 0f;
                if ((position - from).sqrMagnitude > radius * radius)
                {
                    continue;
                }

                PlayerStressController stress = player.gameObject.GetComponentInParent<PlayerStressController>();
                if (stress != null)
                {
                    _inside.Add(stress);
                }
            }

            _players.Clear();

            foreach (PlayerStressController stress in _inside)
            {
                stress.SetSourceRate(_owner, rate, true);
                if (_affected.Add(stress))
                {
                    AILog.Log(AILog.GHOST, _owner.GhostId, $"{stress.name} entered presence ({radius:F1}m, stress +{rate}/s)");
                }
            }

            _affected.RemoveWhere(stress =>
            {
                if (_inside.Contains(stress))
                {
                    return false;
                }

                if (stress != null)
                {
                    stress.RemoveSource(_owner);
                    AILog.Log(AILog.GHOST, _owner.GhostId, $"{stress.name} left presence");
                }

                return true;
            });
        }

        public void Clear()
        {
            foreach (PlayerStressController stress in _affected)
            {
                if (stress != null)
                {
                    stress.RemoveSource(_owner);
                }
            }

            _affected.Clear();
            _inside.Clear();
        }
    }
}
