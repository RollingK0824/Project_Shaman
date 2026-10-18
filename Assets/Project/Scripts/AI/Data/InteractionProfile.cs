using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "InteractionProfile", menuName = "ProjectShaman/AI/Interaction Profile")]
    public class InteractionProfile : ScriptableObject
    {
        [SerializeField] private string _label = "Interaction";
        [SerializeField] private InteractionKind _kind = InteractionKind.GhostTrace;

        [Header("Priority")]
        [SerializeField, Range(0, 100)] private int _priority = 50;
        [SerializeField] private InteractionResponse _maxResponse = InteractionResponse.Approach;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float _durationMin = 3f;
        [SerializeField, Min(0f)] private float _durationMax = 5f;
        [SerializeField, Min(0.1f)] private float _approachDistance = 2f;

        [Header("Notice")]
        [SerializeField, Min(0f)] private float _noticeRadius = 0f;

        [Header("Effect")]
        [SerializeField, Min(0f)] private float _witnessStress = 0f;
        [SerializeField, Min(0f)] private float _detailedWitnessStress = 0f;

        public string Label => string.IsNullOrEmpty(_label) ? name : _label;
        public InteractionKind Kind => _kind;
        public int Priority => _priority;
        public InteractionResponse MaxResponse => _maxResponse;
        public float DurationMin => _durationMin;
        public float DurationMax => Mathf.Max(_durationMin, _durationMax);
        public float ApproachDistance => _approachDistance;
        public float NoticeRadius => _noticeRadius;
        public bool HasNoticeRadius => _noticeRadius > 0f;
        public float WitnessStress => _witnessStress;
        public float DetailedWitnessStress => _detailedWitnessStress;
    }
}
