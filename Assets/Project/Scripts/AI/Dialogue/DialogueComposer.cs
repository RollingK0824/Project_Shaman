using System;
using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.Map;

namespace ProjectShaman.AI.Dialogue
{
    // Turns structured memories into sentences. A witness never names the actor of an anomaly,
    // only the traits it noticed (TraitMask); absence names the missing person.
    public static class DialogueComposer
    {
        public const int TRAIT_GENDER = 1;
        public const int TRAIT_AGE = 2;
        public const int TRAIT_CLASS = 4;

        public static string DescribeFact(in MemoryEntry fact, int currentDay, System.Random random)
        {
            string prefix = fact.Kind == MemoryKind.TraceWitness ? "trace"
                : fact.Kind == MemoryKind.Absent ? "absent"
                : fact.Kind == MemoryKind.Death ? "death"
                : "testimony";
            string mode = fact.Source == MemorySource.Direct ? "seen" : fact.Hops <= 1 ? "heard" : "rumor";
            string template = DialogueLibrary.Line($"{prefix}.{mode}", random);
            MemoryEntry copy = fact;

            return TextTemplate.Fill(template, key =>
            {
                switch (key)
                {
                    case "when": return When(copy.DayCount, currentDay);
                    case "area": return AreaName(copy.AreaId, copy.Position);
                    case "actor": return IsNamed(copy.Kind) ? NameOf(copy.ActorId) : Person(copy.ActorId, copy.TraitMask);
                    case "verb": return VerbPhrase(copy.VerbId, copy.ObjectId);
                    case "object": return copy.Kind == MemoryKind.TraceWitness && !string.IsNullOrEmpty(copy.ActorId)
                        ? $"{NameOf(copy.ActorId)}의 {ObjectName(copy.ObjectId)}"
                        : ObjectName(copy.ObjectId);
                    case "source": return NameOf(copy.SourceId);
                    default: return null;
                }
            });
        }

        // Absence and death are told by name: neither is evidence of possession
        public static bool IsNamed(MemoryKind kind)
        {
            return kind == MemoryKind.Absent || kind == MemoryKind.Death;
        }

        public static string DescribeToolSighting(in ToolSighting sighting, System.Random random)
        {
            string tool = AIDisplayNames.Tool(sighting.TypeId);

            if (!string.IsNullOrEmpty(sighting.HolderId))
            {
                return Fill(DialogueLibrary.Line("tool.held", random), "holder", NameOf(sighting.HolderId), "tool", tool);
            }

            string where = MapAreaRegistry.NameOf(sighting.AreaId) ?? MapAreaRegistry.Describe(sighting.Position);
            return string.IsNullOrEmpty(where)
                ? Fill(DialogueLibrary.Line("tool.near", random), "tool", tool)
                : Fill(DialogueLibrary.Line("tool.place", random), "area", where, "tool", tool);
        }

        public static string Fill(string template, params string[] pairs)
        {
            return TextTemplate.Fill(template, key =>
            {
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    if (pairs[i] == key)
                    {
                        return pairs[i + 1];
                    }
                }

                return null;
            });
        }

        // "양반 영감", "아낙", "노인", "사람" — built only from the traits the witness noticed
        public static string Person(string villagerId, int traitMask)
        {
            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            if (registry == null || !registry.TryGet(villagerId, out IVillagerView view))
            {
                return DialogueLibrary.Noun("person.any", "사람");
            }

            bool gender = (traitMask & TRAIT_GENDER) != 0;
            bool age = (traitMask & TRAIT_AGE) != 0;
            string core;

            if (gender && age)
            {
                core = DialogueLibrary.Noun($"person.{view.AgeGroup}.{view.Gender}");
            }
            else if (age)
            {
                core = DialogueLibrary.Noun($"person.{view.AgeGroup}");
            }
            else if (gender)
            {
                core = DialogueLibrary.Noun($"person.{view.Gender}");
            }
            else
            {
                core = null;
            }

            core = core ?? DialogueLibrary.Noun("person.any", "사람");
            return (traitMask & TRAIT_CLASS) != 0 ? $"{DialogueLibrary.Noun($"class.{view.SocialClass}", string.Empty)} {core}".Trim() : core;
        }

        // Each retelling loses a detail: one trait at the first hop, all of them after that
        public static int BlurTraits(int traitMask, int hops, System.Random random)
        {
            if (hops >= 2 || traitMask == 0)
            {
                return 0;
            }

            List<int> bits = new List<int>();
            for (int bit = 1; bit <= TRAIT_CLASS; bit <<= 1)
            {
                if ((traitMask & bit) != 0)
                {
                    bits.Add(bit);
                }
            }

            return bits.Count <= 1 ? traitMask : traitMask & ~bits[random.Next(bits.Count)];
        }

        public static string VerbPhrase(string verbId, string objectId)
        {
            string objectName = ObjectName(objectId);
            return TextTemplate.Fill(DialogueLibrary.Verb(verbId), key => key == "object" ? objectName : null);
        }

        public static string ObjectName(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return "무언가";
            }

            return Enum.TryParse(objectId, out GhostTraceKind kind) ? AIDisplayNames.Trace(kind) : AIDisplayNames.Tool(objectId);
        }

        public static string AreaName(string areaId, UnityEngine.Vector3 position)
        {
            return MapAreaRegistry.NameOf(areaId) ?? MapAreaRegistry.Describe(position) ?? DialogueLibrary.Noun("place.unknown", "마을 어귀");
        }

        public static string NameOf(string villagerId)
        {
            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            return registry != null && registry.TryGet(villagerId, out IVillagerView view) && !string.IsNullOrEmpty(view.DisplayName) ? view.DisplayName : "누군가";
        }

        private static string When(int factDay, int currentDay)
        {
            int days = currentDay - factDay;
            return DialogueLibrary.Noun(days <= 0 ? "when.0" : days == 1 ? "when.1" : "when.n", string.Empty);
        }
    }
}
