using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Dialogue
{
    // Sentence data for villager speech (Resources/Data/AIDialogue.json). Lines with the same id are variants.
    // Meaning stays in ids (facts, verbs, traits); only this data and TextTemplate's particle helper are language specific.
    public static class DialogueLibrary
    {
        private const string RESOURCE_PATH = "Data/AIDialogue";

        [Serializable]
        private class Entry
        {
            public string id;
            public string text;
        }

        [Serializable]
        private class Data
        {
            public List<Entry> lines = new List<Entry>();
            public List<Entry> verbs = new List<Entry>();
            public List<Entry> nouns = new List<Entry>();
        }

        private static Dictionary<string, List<string>> _lines;
        private static Dictionary<string, string> _verbs;
        private static Dictionary<string, string> _nouns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _lines = null;
            _verbs = null;
            _nouns = null;
        }

        public static string Line(string id, System.Random random)
        {
            EnsureLoaded();
            if (!_lines.TryGetValue(id, out List<string> variants) || variants.Count == 0)
            {
                AILog.Warn(AILog.INTERACTION, $"Dialogue line '{id}' missing in {RESOURCE_PATH}");
                return string.Empty;
            }

            return variants[random != null ? random.Next(variants.Count) : 0];
        }

        public static bool HasLine(string id)
        {
            EnsureLoaded();
            return _lines.ContainsKey(id);
        }

        public static string Verb(string verbId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(verbId) && _verbs.TryGetValue(verbId, out string text) ? text : "이상한 짓을 하는";
        }

        public static string Noun(string id, string fallback = null)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(id) && _nouns.TryGetValue(id, out string text) ? text : fallback;
        }

        private static void EnsureLoaded()
        {
            if (_lines != null)
            {
                return;
            }

            _lines = new Dictionary<string, List<string>>();
            _verbs = new Dictionary<string, string>();
            _nouns = new Dictionary<string, string>();

            TextAsset asset = Resources.Load<TextAsset>(RESOURCE_PATH);
            if (asset == null)
            {
                AILog.Error(AILog.INTERACTION, $"Dialogue data not found at Resources/{RESOURCE_PATH}.json");
                return;
            }

            Data data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null)
            {
                AILog.Error(AILog.INTERACTION, "Dialogue data could not be parsed");
                return;
            }

            foreach (Entry entry in data.lines)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    continue;
                }

                if (!_lines.TryGetValue(entry.id, out List<string> variants))
                {
                    variants = new List<string>();
                    _lines[entry.id] = variants;
                }

                variants.Add(entry.text ?? string.Empty);
            }

            foreach (Entry entry in data.verbs)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.id))
                {
                    _verbs[entry.id] = entry.text;
                }
            }

            foreach (Entry entry in data.nouns)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.id))
                {
                    _nouns[entry.id] = entry.text;
                }
            }

            AILog.Log(AILog.INTERACTION, $"Dialogue data loaded (lines={_lines.Count}, verbs={_verbs.Count}, nouns={_nouns.Count})");
        }
    }
}
