using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI.Memory
{
    [Serializable]
    public struct ToolSighting
    {
        public string InstanceId;
        public string TypeId;
        public Vector3 Position;
        public string HolderId;
        public int DayCount;
        public float SeenTime;

        public override string ToString()
        {
            string where = string.IsNullOrEmpty(HolderId) ? Position.ToString("F1") : $"held by {HolderId}";
            return $"{TypeId}:{InstanceId} @ {where} (Day{DayCount} {SeenTime:F0}s)";
        }
    }

    public class ToolSightingStore
    {
        private readonly Dictionary<string, List<ToolSighting>> _byType = new Dictionary<string, List<ToolSighting>>();
        private readonly int _maxPerType;

        public ToolSightingStore(int maxPerType)
        {
            _maxPerType = Mathf.Max(1, maxPerType);
        }

        public void Record(ToolSighting sighting)
        {
            if (!_byType.TryGetValue(sighting.TypeId, out List<ToolSighting> list))
            {
                list = new List<ToolSighting>();
                _byType[sighting.TypeId] = list;
            }

            list.RemoveAll(s => s.InstanceId == sighting.InstanceId);
            list.Insert(0, sighting);

            while (list.Count > _maxPerType)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        public IReadOnlyList<ToolSighting> Get(string typeId)
        {
            if (!string.IsNullOrEmpty(typeId) && _byType.TryGetValue(typeId, out List<ToolSighting> list))
            {
                return list;
            }

            return Array.Empty<ToolSighting>();
        }

        public void WriteDebug(List<string> output)
        {
            output.Clear();

            foreach (KeyValuePair<string, List<ToolSighting>> pair in _byType)
            {
                foreach (ToolSighting sighting in pair.Value)
                {
                    output.Add(sighting.ToString());
                }
            }
        }
    }
}
