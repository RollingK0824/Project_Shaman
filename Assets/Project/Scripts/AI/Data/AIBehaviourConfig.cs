using UnityEngine;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "AIBehaviourConfig", menuName = "ProjectShaman/AI/Behaviour Config")]
    public class AIBehaviourConfig : ScriptableObject
    {
        [SerializeField] private int _cycleDays = 3;
        [SerializeField] private int _minRestPerDay = 1;
        [SerializeField] private float _maxStartOffsetSeconds = 40f;
        [SerializeField] private float _minRemainingSecondsToRun = 20f;
        [SerializeField] private float _homeReturnMarginSeconds = 10f;
        [SerializeField] private float _toolTidySecondsEstimate = 5f;
        [SerializeField] private float _walkSpeedEstimate = 3.5f;

        public int CycleDays => _cycleDays;
        public int MinRestPerDay => _minRestPerDay;
        public float MaxStartOffsetSeconds => _maxStartOffsetSeconds;
        public float MinRemainingSecondsToRun => _minRemainingSecondsToRun;
        public float HomeReturnMarginSeconds => _homeReturnMarginSeconds;
        public float ToolTidySecondsEstimate => _toolTidySecondsEstimate;
        public float WalkSpeedEstimate => _walkSpeedEstimate;
    }
}
