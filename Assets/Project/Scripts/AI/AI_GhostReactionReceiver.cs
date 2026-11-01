using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Core))]
    public class AI_GhostReactionReceiver : MonoBehaviour, ISleepParalyzable
    {
        [SerializeField] private bool _isMarked;

        [Header("Runtime (server only)")]
        [SerializeField] private int _paralyzedNight = -1;

        private AI_Core _core;
        private AI_Memory _memory;

        public GhostTargetKind Kind => GhostTargetKind.Villager;
        public bool IsMarked => _isMarked;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _memory = GetComponent<AI_Memory>();
        }

        private void OnEnable()
        {
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            GhostTargetRegistry.Unregister(this);
        }

        public void SetMarked(bool marked)
        {
            _isMarked = marked;
            AILog.Log(AILog.GHOST, _core.LogId, $"Marked={marked}");
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            if (_core.Profile == null || _core.Profile.IsPossessed || context.Ledger == null || !context.Ledger.IsAlive(_core))
            {
                return false;
            }

            // A villager at a house with a barrier talisman is off limits, even when its home point sits outside the house box
            float homeDistance = context.Ledger.Config.SleepParalysisHomeDistance;
            if (IsAtHome(homeDistance) && IsHomeProtected())
            {
                return false;
            }

            if (context.Verb == GhostActionVerb.SleepParalysis)
            {
                return _paralyzedNight != context.NightDay && IsAsleep() && IsAtHome(homeDistance);
            }

            return true;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return _isMarked ? 1 : 0;
        }

        public void ReceiveSleepParalysis(in GhostActionContext context)
        {
            string villagerId = _core.Profile.VillagerId;
            _paralyzedNight = context.NightDay;

            if (context.Action != null)
            {
                context.Ledger.AddVictimStress(villagerId, context.Action.VillagerStress, context.ActionId);

                if (context.Action.HasDayEffect)
                {
                    context.Ledger.ScheduleDayEffect(villagerId, context.Action.DayEffect);
                }
            }

            if (_memory != null)
            {
                _memory.RecordSleepParalysis(context.ActionId);
            }

            AILog.Log(AILog.GHOST, context.GhostId, $"Sleep paralysis on {villagerId}");
        }

        // Really asleep (AIManager puts a villager to sleep in its house at night): one still up cannot be pinned in its
        // sleep. The schedule tab's bed hour only shows it (C131)
        private bool IsAsleep()
        {
            return _core.IsAsleep;
        }

        private bool IsHomeProtected()
        {
            return PlaceRegistry.TryGet(_core.Profile.PublicInfo.HomePlaceId, out PlaceArea home) && HouseBarrierWard.IsProtecting(home);
        }

        private bool IsAtHome(float distance)
        {
            Vector3 offset = transform.position - _core.Profile.HomePosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= distance * distance;
        }
    }
}
