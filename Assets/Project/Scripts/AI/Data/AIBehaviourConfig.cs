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

        public int CycleDays => _cycleDays;
        public int MinRestPerDay => _minRestPerDay;
        public float MaxStartOffsetRatio => _maxStartOffsetRatio;
        public float MinRemainingRatio => _minRemainingRatio;
        public float HomeReturnMarginSeconds => _homeReturnMarginSeconds;
        public float ToolTidySecondsEstimate => _toolTidySecondsEstimate;
        public float WalkSpeedEstimate => _walkSpeedEstimate;
    }
}
