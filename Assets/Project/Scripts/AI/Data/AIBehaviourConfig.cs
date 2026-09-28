using UnityEngine;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "AIBehaviourConfig", menuName = "ProjectShaman/AI/Behaviour Config")]
    public class AIBehaviourConfig : ScriptableObject
    {
        [SerializeField] private int _cycleDays = 3;
        [SerializeField] private int _minRestPerDay = 1;
        [SerializeField, Range(0f, 1f)] private float _maxStartOffsetRatio = 0.33f;
        [SerializeField, Range(0f, 1f)] private float _minRemainingRatio = 0.17f;
        [SerializeField] private float _homeReturnMarginSeconds = 10f;
        [SerializeField] private float _toolTidySecondsEstimate = 5f;
        [SerializeField] private float _walkSpeedEstimate = 3.5f;

        [Header("Tool")]
        [SerializeField] private float _toolSightRadius = 8f;
        [SerializeField] private float _toolSightInterval = 1f;
        [SerializeField] private float _toolPickupDistance = 1.5f;
        [SerializeField] private int _toolSightingsPerType = 2;
        [SerializeField, Range(0f, 1f)] private float _toolSearchTimeRatio = 0.4f;
        [SerializeField] private float _tidyRelaxedMaxDistance = 15f;
        [SerializeField] private float _tidyRushedMaxDistance = 35f;

        [Header("Ghost / Corruption")]
        [SerializeField] private float _initialYin = 30f;
        [SerializeField] private float _yinForMaxFrequency = 100f;
        [SerializeField] private float _minimumYin = 20f;
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtZeroYin = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtMaxYin = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _lastSlotChanceMultiplier = 0.5f;
        [SerializeField] private int _minCorruptionsPerDay = 1;
        [SerializeField, Range(0f, 1f)] private float _timeDelayMinRatio = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _timeDelayMaxRatio = 0.25f;
        [SerializeField] private int _maxSymptomsPerRestSlot = 3;
        [SerializeField] private float _emptyHandToolYinThreshold = 80f;
        [SerializeField] private float _toolCorruptionSearchRadius = 25f;

        public int CycleDays => _cycleDays;
        public int MinRestPerDay => _minRestPerDay;
        public float MaxStartOffsetRatio => _maxStartOffsetRatio;
        public float MinRemainingRatio => _minRemainingRatio;
        public float HomeReturnMarginSeconds => _homeReturnMarginSeconds;
        public float ToolTidySecondsEstimate => _toolTidySecondsEstimate;
        public float WalkSpeedEstimate => _walkSpeedEstimate;
        public float ToolSightRadius => _toolSightRadius;
        public float ToolSightInterval => _toolSightInterval;
        public float ToolPickupDistance => _toolPickupDistance;
        public int ToolSightingsPerType => _toolSightingsPerType;
        public float ToolSearchTimeRatio => _toolSearchTimeRatio;
        public float TidyRelaxedMaxDistance => _tidyRelaxedMaxDistance;
        public float TidyRushedMaxDistance => _tidyRushedMaxDistance;
        public float InitialYin => _initialYin;
        public float YinForMaxFrequency => _yinForMaxFrequency;
        public float MinimumYin => _minimumYin;
        public float CorruptionChanceAtZeroYin => _corruptionChanceAtZeroYin;
        public float CorruptionChanceAtMaxYin => _corruptionChanceAtMaxYin;
        public float LastSlotChanceMultiplier => _lastSlotChanceMultiplier;
        public int MinCorruptionsPerDay => _minCorruptionsPerDay;
        public float TimeDelayMinRatio => _timeDelayMinRatio;
        public float TimeDelayMaxRatio => _timeDelayMaxRatio;
        public int MaxSymptomsPerRestSlot => _maxSymptomsPerRestSlot;
        public float EmptyHandToolYinThreshold => _emptyHandToolYinThreshold;
        public float ToolCorruptionSearchRadius => _toolCorruptionSearchRadius;
    }
}
