using System.Collections.Generic;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public struct PlannedSymptom
    {
        public int Slot;
        public float SlotFraction;
        public SymptomType Type;
        public bool HasFired;
    }

    public class CorruptionPlan
    {
        private readonly Dictionary<int, CorruptionAxis> _slotAxes = new Dictionary<int, CorruptionAxis>();

        public int DayCount { get; }
        public float MorningYin { get; }
        public List<PlannedSymptom> Symptoms { get; } = new List<PlannedSymptom>();

        public CorruptionPlan(int dayCount, float morningYin)
        {
            DayCount = dayCount;
            MorningYin = morningYin;
        }

        public void SetSlotAxis(int slot, CorruptionAxis axis)
        {
            _slotAxes[slot] = axis;
        }

        public CorruptionAxis GetSlotAxis(int slot)
        {
            return _slotAxes.TryGetValue(slot, out CorruptionAxis axis) ? axis : CorruptionAxis.None;
        }

        public int CorruptedSlotCount
        {
            get
            {
                int count = 0;
                foreach (CorruptionAxis axis in _slotAxes.Values)
                {
                    if (axis != CorruptionAxis.None)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void WriteDebug(List<string> output)
        {
            output.Clear();
            output.Add($"Day{DayCount} yin={MorningYin:F0} corrupted={CorruptedSlotCount}");

            foreach (KeyValuePair<int, CorruptionAxis> pair in _slotAxes)
            {
                output.Add($"S{pair.Key}: {pair.Value}");
            }

            foreach (PlannedSymptom symptom in Symptoms)
            {
                output.Add($"S{symptom.Slot} @{symptom.SlotFraction:P0}: {symptom.Type}{(symptom.HasFired ? " (fired)" : string.Empty)}");
            }
        }
    }
}
