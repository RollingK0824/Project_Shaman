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

        // Structured fact (Witness / TraceWitness / Absent): who did what with what, where, and how we know it
        public string ActorId;
        public string VerbId;
        public string ObjectId;
        public string AreaId;
        public string SourceId;     // villager we heard it from (Source == Heard)
        public int Hops;            // 0 = seen, 1 = heard from a witness, ...
        public int TraitMask;       // which actor traits we can tell: 1 gender, 2 age, 4 class
        public bool IsAnomalous;

        public string FactKey => $"{Kind}|{ActorId ?? SubjectId}|{VerbId}|{ObjectId}|{DayCount}";

        public override string ToString()
        {
            string place = string.IsNullOrEmpty(PlaceId) ? Position.ToString("F1") : PlaceId;
            string detail = string.IsNullOrEmpty(Detail) ? string.Empty : $" {Detail}";
            string fact = string.IsNullOrEmpty(VerbId) ? string.Empty : $" [{ActorId} {VerbId} {ObjectId} traits={TraitMask} hops={Hops}{(string.IsNullOrEmpty(SourceId) ? string.Empty : $" from {SourceId}")}]";
            return $"Day{DayCount} {RecordedTime:F0}s {Kind}/{Source} {SubjectId} @ {place}{detail}{fact}";
        }
    }
}
