using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Perception;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.Stress;

namespace ProjectShaman.AI.Observation
{
    // Server only. Turns live observables into witness memories (radius → field of view → line of sight),
    // registers the observables villagers produce, and adds look-alike behaviour for anxious villagers
    // so a strange sighting is never proof of possession on its own.
    public sealed class ObservationService
    {
        private const float SYMPTOM_SECONDS = 6f;
        private const float MUTTER_SECONDS = 5f;
        private const float ACTOR_EYE_OFFSET = 1.2f;

        private static readonly string[] MUTTER_LINES = { "에휴… 이를 어쩐다…", "아이고, 머리야…", "누가 날 보는 것 같구먼…" };

        private readonly AIBehaviourConfig _config;
        private readonly System.Random _random;
        private readonly Dictionary<string, float> _nextMutterTime = new Dictionary<string, float>();
        private float _nextScanTime;

        public ObservationService(AIBehaviourConfig config, int seed)
        {
            _config = config;
            _random = new System.Random(seed);
            ObservableRegistry.Clear();
        }

        public void Clear()
        {
            ObservableRegistry.Clear();
            _nextMutterTime.Clear();
        }

        public static string SymptomVerb(SymptomType symptom)
        {
            switch (symptom)
            {
                case SymptomType.Tremble: return ObservationVerbs.TREMBLE;
                case SymptomType.StareIntoVoid: return ObservationVerbs.STARE_VOID;
                case SymptomType.Nonsense: return ObservationVerbs.TALK_NONSENSE;
                case SymptomType.Grotesque: return ObservationVerbs.WALK_GROTESQUE;
                case SymptomType.NeckTwist: return ObservationVerbs.NECK_TWIST;
                case SymptomType.WaistBend: return ObservationVerbs.WAIST_BEND;
                case SymptomType.NoShadow: return ObservationVerbs.NO_SHADOW;
                case SymptomType.PaleSkin: return ObservationVerbs.PALE_SKIN;
                case SymptomType.BlackEyes: return ObservationVerbs.BLACK_EYES;
                case SymptomType.Freeze: return ObservationVerbs.FREEZE;
                case SymptomType.GlanceBack: return ObservationVerbs.GLANCE_BACK;
                default: return ObservationVerbs.EMPTY_HAND_WORK;
            }
        }

        public void OnSymptom(AI_Core core, SymptomType symptom)
        {
            ObservableRegistry.Register(core.LogId, core.transform, SymptomVerb(symptom), null, SYMPTOM_SECONDS);
        }

        // A corrupted routine is visible while the villager is actually on it
        public void OnRoutineApplied(AI_Core core, AI_Interaction interaction, in ResolvedRoutine routine, float remainingSeconds)
        {
            if (!routine.IsCorrupted || remainingSeconds <= 0f)
            {
                return;
            }

            int serial = routine.Serial;
            string verb;
            string objectId = null;
            float seconds = remainingSeconds;

            switch (routine.CorruptionAxis)
            {
                case CorruptionAxis.Tool:
                    verb = ObservationVerbs.WORK_WRONG_TOOL;
                    objectId = routine.ToolTypeOverride;
                    break;
                case CorruptionAxis.Action:
                    verb = ObservationVerbs.WORK_WRONG_ACTION;
                    break;
                case CorruptionAxis.Place:
                    verb = ObservationVerbs.WORK_WRONG_PLACE;
                    break;
                case CorruptionAxis.Time:
                    verb = ObservationVerbs.STAND_STILL;
                    seconds = Mathf.Min(remainingSeconds, Mathf.Max(1f, routine.StartDelaySeconds));
                    break;
                default:
                    return;
            }

            bool needsWork = routine.CorruptionAxis != CorruptionAxis.Time;
            ObservableRegistry.Register(core.LogId, core.transform, verb, objectId, seconds, true,
                () => core != null && core.CurrentRoutine.Serial == serial && (!needsWork || IsWorking(interaction)));
        }

        public void OnVillagerRemoved(string villagerId)
        {
            ObservableRegistry.RemoveActor(villagerId);
            _nextMutterTime.Remove(villagerId);
        }

        public void Tick(IReadOnlyList<AIManager.VillagerRuntime> villagers)
        {
            float now = Time.time;
            if (now < _nextScanTime)
            {
                return;
            }

            _nextScanTime = now + _config.ObservationInterval;
            ObservableRegistry.Prune();

            foreach (AIManager.VillagerRuntime villager in villagers)
            {
                if (villager.Core == null || !villager.Core.IsSimulating)
                {
                    continue;
                }

                TickAnxious(villager, now);
                Scan(villager);
                ScanVillagers(villager, villagers);
            }
        }

        private void Scan(AIManager.VillagerRuntime villager)
        {
            AI_Perception perception = villager.Perception;
            if (perception == null || villager.Memory == null || ObservableRegistry.All.Count == 0)
            {
                return;
            }

            float radiusSqr = _config.ObservationRadius * _config.ObservationRadius;
            Vector3 from = villager.Core.transform.position;

            foreach (Observable observable in ObservableRegistry.All)
            {
                if (observable.ActorId == villager.Id || !observable.IsLive)
                {
                    continue;
                }

                Vector3 actorPosition = observable.Position;
                if ((actorPosition - from).sqrMagnitude > radiusSqr || !perception.CanSee(actorPosition + Vector3.up * ACTOR_EYE_OFFSET))
                {
                    continue;
                }

                villager.Memory.RecordWitness(observable, PickTraitMask());
            }
        }

        private void ScanVillagers(AIManager.VillagerRuntime villager, IReadOnlyList<AIManager.VillagerRuntime> villagers)
        {
            AI_Perception perception = villager.Perception;
            if (perception == null || villager.Memory == null)
            {
                return;
            }

            float radiusSqr = _config.ObservationRadius * _config.ObservationRadius;
            Vector3 from = villager.Core.transform.position;

            foreach (AIManager.VillagerRuntime other in villagers)
            {
                if (other == villager || other.Core == null)
                {
                    continue;
                }

                // Seen today already: looked at again only to remember who was there during this routine (the alibi),
                // at most a few per routine, so the line-of-sight tests stay bounded
                bool seenToday = villager.Memory.HasSeenToday(other.Id);
                bool wantsCompanion = villager.Memory.WantsCompanion(other.Id);
                if (seenToday && !wantsCompanion)
                {
                    continue;
                }

                Vector3 position = other.Core.transform.position;
                if ((position - from).sqrMagnitude > radiusSqr || !perception.CanSee(position + Vector3.up * ACTOR_EYE_OFFSET))
                {
                    continue;
                }

                if (!seenToday)
                {
                    villager.Memory.RecordSeenVillager(other.Id);
                }

                if (wantsCompanion)
                {
                    villager.Memory.RecordCompanion(other.Id);
                }
            }
        }

        // Anxious villagers now and then mutter or tremble: the same sentence a witness would use for a symptom
        private void TickAnxious(AIManager.VillagerRuntime villager, float now)
        {
            AI_Stress stress = villager.Stress;
            if (stress == null || stress.State == VillagerStressState.Calm || villager.IsHomeOrdered)
            {
                return;
            }

            if (!_nextMutterTime.TryGetValue(villager.Id, out float next))
            {
                _nextMutterTime[villager.Id] = now + RandomRange(0.3f, 1f) * _config.AnxiousMutterInterval;
                return;
            }

            if (now < next)
            {
                return;
            }

            _nextMutterTime[villager.Id] = now + RandomRange(0.7f, 1.3f) * _config.AnxiousMutterInterval;
            if (_random.NextDouble() > _config.AnxiousMutterChance)
            {
                return;
            }

            bool mutter = _random.NextDouble() < 0.6;
            ObservableRegistry.Register(villager.Id, villager.Core.transform, mutter ? ObservationVerbs.TALK_NONSENSE : ObservationVerbs.TREMBLE, null, MUTTER_SECONDS);

            AI_Conversation conversation = villager.Conversation;
            if (mutter && conversation != null)
            {
                conversation.Say(MUTTER_LINES[_random.Next(MUTTER_LINES.Length)], "anxious mutter");
            }

            AILog.Log(AILog.STRESS, villager.Id, $"Anxious look-alike behaviour ({(mutter ? "mutter" : "tremble")})");
        }

        // Each witness keeps two of gender / age / class (config), never the name
        private int PickTraitMask()
        {
            int count = _random.Next(_config.WitnessTraitMin, _config.WitnessTraitMax + 1);
            int mask = 0;

            while (CountBits(mask) < count)
            {
                mask |= 1 << _random.Next(3);
            }

            return mask;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            for (; value != 0; value &= value - 1)
            {
                count++;
            }

            return count;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private static bool IsWorking(AI_Interaction interaction)
        {
            return interaction == null || interaction.CreateContext().Activity == VillagerActivity.Work;
        }
    }

    public static class ObservationVerbs
    {
        public const string TREMBLE = "tremble";
        public const string STARE_VOID = "stare_void";
        public const string TALK_NONSENSE = "talk_nonsense";
        public const string WALK_GROTESQUE = "walk_grotesque";
        public const string NECK_TWIST = "neck_twist";
        public const string WAIST_BEND = "waist_bend";
        public const string NO_SHADOW = "no_shadow";
        public const string PALE_SKIN = "pale_skin";
        public const string BLACK_EYES = "black_eyes";
        public const string FREEZE = "freeze";
        public const string GLANCE_BACK = "glance_back";
        public const string EMPTY_HAND_WORK = "empty_hand_work";
        public const string WORK_WRONG_TOOL = "work_wrong_tool";
        public const string WORK_WRONG_ACTION = "work_wrong_action";
        public const string WORK_WRONG_PLACE = "work_wrong_place";
        public const string STAND_STILL = "stand_still";
        public const string ABSENT = "absent";
    }
}
