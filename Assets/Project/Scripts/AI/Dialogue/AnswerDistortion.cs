using System;
using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.Stress;
using ProjectShaman.Map;

namespace ProjectShaman.AI.Dialogue
{
    // Unreliable witnesses twist one axis of what they tell the player, the way possession twists one axis of a routine.
    // The host lies (and always pins its own deeds on someone else); a stressed villager "remembers" wrongly
    // (stress hallucination). Both read the same, so a wrong detail never tells which one you were talking to.
    // The twist is fixed per speaker and fact: asking twice gets the same wrong answer.
    public static class AnswerDistortion
    {
        private enum Axis
        {
            Actor,
            Place,
            Object,
            Time
        }

        private static readonly List<Axis> _axes = new List<Axis>();
        private static readonly List<string> _candidates = new List<string>();

        public static bool IsUnreliable(AI_Conversation speaker, out bool possessed)
        {
            possessed = speaker != null && speaker.Core != null && speaker.Core.Profile != null && speaker.Core.Profile.IsPossessed;
            if (possessed)
            {
                return true;
            }

            AI_Stress stress = speaker != null ? speaker.GetComponent<AI_Stress>() : null;
            return stress != null && stress.State != VillagerStressState.Calm;
        }

        public static MemoryEntry Apply(AI_Conversation speaker, in MemoryEntry fact)
        {
            if (!IsUnreliable(speaker, out bool possessed))
            {
                return fact;
            }

            System.Random random = new System.Random(StableHash(speaker.VillagerId + "|" + fact.FactKey));
            MemoryEntry twisted = fact;
            bool selfBlame = possessed && fact.ActorId == speaker.VillagerId;

            _axes.Clear();
            if (selfBlame || (!string.IsNullOrEmpty(fact.ActorId) && (fact.TraitMask != 0 || DialogueComposer.IsNamed(fact.Kind))))
            {
                _axes.Add(Axis.Actor);
            }

            if (MapAreaRegistry.All.Count > 1)
            {
                _axes.Add(Axis.Place);
            }

            if (!string.IsNullOrEmpty(fact.ObjectId))
            {
                _axes.Add(Axis.Object);
            }

            _axes.Add(Axis.Time);

            Axis axis = selfBlame ? Axis.Actor : _axes[random.Next(_axes.Count)];

            switch (axis)
            {
                case Axis.Actor:
                    twisted.ActorId = OtherVillager(random, fact.ActorId, speaker.VillagerId) ?? fact.ActorId;
                    if (selfBlame && twisted.TraitMask == 0)
                    {
                        // The host makes sure the blame lands on someone recognisable
                        twisted.TraitMask = DialogueComposer.TRAIT_GENDER | DialogueComposer.TRAIT_AGE;
                    }
                    break;
                case Axis.Place:
                    twisted.AreaId = OtherArea(random, fact.AreaId) ?? fact.AreaId;
                    break;
                case Axis.Object:
                    twisted.ObjectId = OtherObject(random, fact.ObjectId);
                    break;
                case Axis.Time:
                    twisted.DayCount = fact.DayCount - 1 - random.Next(2);
                    break;
            }

            AILog.Log(AILog.INTERACTION, speaker.VillagerId, $"Answer twisted ({(possessed ? "host lie" : "stress hallucination")}, {axis}{(selfBlame ? ", self-blame" : string.Empty)}): {fact.FactKey}");
            return twisted;
        }

        public static ToolSighting Apply(AI_Conversation speaker, in ToolSighting sighting)
        {
            if (!IsUnreliable(speaker, out bool possessed))
            {
                return sighting;
            }

            System.Random random = new System.Random(StableHash(speaker.VillagerId + "|tool|" + sighting.InstanceId + "|" + sighting.DayCount));
            ToolSighting twisted = sighting;

            if (!string.IsNullOrEmpty(sighting.HolderId))
            {
                twisted.HolderId = OtherVillager(random, sighting.HolderId, speaker.VillagerId) ?? sighting.HolderId;
            }
            else
            {
                twisted.AreaId = OtherArea(random, sighting.AreaId) ?? sighting.AreaId;
            }

            AILog.Log(AILog.INTERACTION, speaker.VillagerId, $"Tool answer twisted ({(possessed ? "host lie" : "stress hallucination")}): {sighting}");
            return twisted;
        }

        // "Who was there with you". The host covers a routine the ghost moved in place or time: it was where the schedule
        // says, and saw nobody (those really there will not name it). Unreliable speakers otherwise drop or swap one
        // companion, fixed per speaker and routine, so one missing name never tells host from stressed villager
        public static void ApplyAlibi(AI_Conversation speaker, RoutineAccount account, List<string> companions, out bool coversCorruption)
        {
            companions.Clear();
            companions.AddRange(account.Companions);
            coversCorruption = false;

            if (!IsUnreliable(speaker, out bool possessed))
            {
                return;
            }

            ResolvedRoutine routine = account.Routine;
            if (possessed && routine.IsCorrupted && (routine.CorruptionAxis == CorruptionAxis.Place || routine.CorruptionAxis == CorruptionAxis.Time))
            {
                companions.Clear();
                coversCorruption = true;
                AILog.Log(AILog.INTERACTION, speaker.VillagerId, $"Alibi covers corrupted {routine.Source.RoutineId} ({routine.CorruptionAxis}): scheduled place, nobody");
                return;
            }

            if (companions.Count == 0)
            {
                return;
            }

            System.Random random = new System.Random(StableHash(speaker.VillagerId + "|alibi|" + routine.DayCount + "|" + routine.Serial));
            int index = random.Next(companions.Count);
            string replaced = companions[index];
            string other = random.Next(2) == 0 ? OtherVillager(random, replaced, speaker.VillagerId) : null;

            bool swapped = other != null && !companions.Contains(other);
            if (swapped)
            {
                companions[index] = other;
            }
            else
            {
                companions.RemoveAt(index);
            }

            AILog.Log(AILog.INTERACTION, speaker.VillagerId, $"Alibi twisted ({(possessed ? "host lie" : "stress hallucination")}): {replaced} → {(swapped ? other : "dropped")}");
        }

        private static string OtherVillager(System.Random random, string exclude, string speakerId)
        {
            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            if (registry == null)
            {
                return null;
            }

            _candidates.Clear();
            foreach (IVillagerView view in registry.Villagers)
            {
                if (view.IsAlive && view.VillagerId != exclude && view.VillagerId != speakerId)
                {
                    _candidates.Add(view.VillagerId);
                }
            }

            return _candidates.Count > 0 ? _candidates[random.Next(_candidates.Count)] : null;
        }

        private static string OtherArea(System.Random random, string exclude)
        {
            _candidates.Clear();
            foreach (IMapArea area in MapAreaRegistry.All)
            {
                if (area != null && area.AreaId != exclude)
                {
                    _candidates.Add(area.AreaId);
                }
            }

            return _candidates.Count > 0 ? _candidates[random.Next(_candidates.Count)] : null;
        }

        private static string OtherObject(System.Random random, string objectId)
        {
            if (Enum.TryParse(objectId, out GhostTraceKind kind))
            {
                Array kinds = Enum.GetValues(typeof(GhostTraceKind));
                GhostTraceKind other = (GhostTraceKind)kinds.GetValue(random.Next(kinds.Length));
                return other == kind ? ((GhostTraceKind)kinds.GetValue((Array.IndexOf(kinds, kind) + 1) % kinds.Length)).ToString() : other.ToString();
            }

            IReadOnlyList<string> tools = AIDisplayNames.ToolTypes;
            if (tools.Count < 2)
            {
                return objectId;
            }

            string tool = tools[random.Next(tools.Count)];
            return tool == objectId ? tools[(random.Next(tools.Count - 1) + 1 + IndexOf(tools, tool)) % tools.Count] : tool;
        }

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return i;
                }
            }

            return 0;
        }

        // string.GetHashCode is not stable across runs; this one is
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }

                return hash & 0x7fffffff;
            }
        }
    }
}
