using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Core
{
    // Korean display names for AI-side ids (tool types, trace kinds), shared by dialogue and quest text
    public static class AIDisplayNames
    {
        private static readonly Dictionary<string, string> TOOLS = new Dictionary<string, string>
        {
            { "Hoe", "괭이" }, { "Sickle", "낫" }, { "Axe", "도끼" }, { "Bucket", "물동이" }, { "Rope", "새끼줄" },
            { "Hammer", "망치" }, { "Knife", "칼" }, { "Broom", "빗자루" }, { "Book", "책" }, { "Jar", "항아리" },
            { "Ladle", "국자" }, { "FishingRod", "낚싯대" }, { "Whetstone", "숫돌" }, { "Scraper", "긁개" },
            { "Bow", "활" }, { "Spear", "창" }, { "Brush", "붓" }, { "Bundle", "보따리" }, { "Fan", "부채" },
            { "Mask", "탈" }, { "Scale", "저울" }, { "Teapot", "주전자" }, { "Tray", "쟁반" }
        };

        private static readonly Dictionary<string, string> TRACES = new Dictionary<string, string>
        {
            { nameof(GhostTraceKind.LivestockCorpse), "가축 사체" },
            { nameof(GhostTraceKind.VillagerCorpse), "시신" },
            { nameof(GhostTraceKind.DefiledWell), "더럽혀진 우물" },
            { nameof(GhostTraceKind.DefiledStorage), "더럽혀진 곳간" },
            { nameof(GhostTraceKind.DefiledGuardian), "훼손된 장승" },
            { nameof(GhostTraceKind.DefiledOffering), "더럽혀진 제물" },
            { nameof(GhostTraceKind.ShadowSpot), "음습한 그늘" }
        };

        private static List<string> _toolTypes;

        public static IReadOnlyList<string> ToolTypes => _toolTypes ?? (_toolTypes = new List<string>(TOOLS.Keys));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterQuestNames()
        {
            QuestText.AddNameResolver(Find);
        }

        public static string Tool(string toolType)
        {
            return !string.IsNullOrEmpty(toolType) && TOOLS.TryGetValue(toolType, out string name) ? name : toolType;
        }

        public static string Trace(GhostTraceKind kind)
        {
            return TRACES.TryGetValue(kind.ToString(), out string name) ? name : kind.ToString();
        }

        // Particle by the last syllable's final consonant (괭이를 / 낫을)
        public static string Josa(string word, string afterConsonant, string afterVowel)
        {
            if (string.IsNullOrEmpty(word))
            {
                return afterVowel;
            }

            char last = word[word.Length - 1];
            bool hasFinal = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
            return hasFinal ? afterConsonant : afterVowel;
        }

        private static string Find(string id)
        {
            if (TOOLS.TryGetValue(id, out string tool))
            {
                return tool;
            }

            return TRACES.TryGetValue(id, out string trace) ? trace : null;
        }
    }
}
