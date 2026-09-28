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
    }
}
