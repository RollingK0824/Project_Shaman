using System;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Memory
{
    [Serializable]
    public struct MemoryEntry
    {
        public MemoryKind Kind;
        public MemorySource Source;
        public string SubjectId;
        public string PlaceId;
        public Vector3 Position;
        public string Detail;
        public int DayCount;
        public float RecordedTime;

        public override string ToString()
        {
            string place = string.IsNullOrEmpty(PlaceId) ? Position.ToString("F1") : PlaceId;
            string detail = string.IsNullOrEmpty(Detail) ? string.Empty : $" {Detail}";
            return $"Day{DayCount} {RecordedTime:F0}s {Kind}/{Source} {SubjectId} @ {place}{detail}";
        }
    }
}
