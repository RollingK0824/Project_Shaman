using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Memory
{
    public class EventMemoryStore
    {
        private readonly Dictionary<MemoryKind, List<MemoryEntry>> _byKind = new Dictionary<MemoryKind, List<MemoryEntry>>();
        private readonly int _maxPerKind;
        private readonly int _keepDays;

        public EventMemoryStore(int maxPerKind, int keepDays)
        {
            _maxPerKind = Mathf.Max(1, maxPerKind);
            _keepDays = Mathf.Max(1, keepDays);
        }

        public void Record(MemoryEntry entry)
        {
            if (!_byKind.TryGetValue(entry.Kind, out List<MemoryEntry> list))
            {
                list = new List<MemoryEntry>();
                _byKind[entry.Kind] = list;
            }

            list.Insert(0, entry);

            while (list.Count > _maxPerKind)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        public int Prune(int currentDay)
        {
            int oldestDay = currentDay - _keepDays + 1;
            int removed = 0;

            foreach (List<MemoryEntry> list in _byKind.Values)
            {
                removed += list.RemoveAll(e => e.DayCount < oldestDay);
            }

            return removed;
        }

        public IReadOnlyList<MemoryEntry> Get(MemoryKind kind)
        {
            if (_byKind.TryGetValue(kind, out List<MemoryEntry> list))
            {
                return list;
            }

            return Array.Empty<MemoryEntry>();
        }

        public int RemoveAll(Predicate<MemoryEntry> match)
        {
            int removed = 0;

            foreach (List<MemoryEntry> list in _byKind.Values)
            {
                removed += list.RemoveAll(match);
            }

            return removed;
        }

        public bool ContainsFact(string factKey)
        {
            foreach (List<MemoryEntry> list in _byKind.Values)
            {
                foreach (MemoryEntry entry in list)
                {
                    if (entry.FactKey == factKey)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Witness, trace and absence facts, newest first
        public void CollectFacts(List<MemoryEntry> results)
        {
            results.Clear();
            results.AddRange(Get(MemoryKind.Witness));
            results.AddRange(Get(MemoryKind.TraceWitness));
            results.AddRange(Get(MemoryKind.Absent));
            results.AddRange(Get(MemoryKind.Death));
            results.Sort((a, b) => b.RecordedTime.CompareTo(a.RecordedTime));
        }

        public bool Contains(MemoryKind kind, string subjectId)
        {
            foreach (MemoryEntry entry in Get(kind))
            {
                if (entry.SubjectId == subjectId)
                {
                    return true;
                }
            }

            return false;
        }

        public void WriteDebug(List<string> output)
        {
            output.Clear();

            foreach (List<MemoryEntry> list in _byKind.Values)
            {
                foreach (MemoryEntry entry in list)
                {
                    output.Add(entry.ToString());
                }
            }
        }
    }
}
