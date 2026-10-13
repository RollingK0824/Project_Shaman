using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    [CreateAssetMenu(fileName = "GhostNightAction", menuName = "ProjectShaman/AI/Ghost Night Action")]
    public class GhostNightActionData : ScriptableObject
    {
        [SerializeField] private string _actionId;
        [SerializeField] private GhostActionVerb _verb = GhostActionVerb.Attack;
        [SerializeField] private GhostTargetKind[] _targetKinds = { GhostTargetKind.Villager };
        [SerializeField] private bool _preferMarked;

        [Header("Condition / Cost")]
        [SerializeField] private bool _hunterOnly;
        [SerializeField] private float _minYin;
        [SerializeField] private float _yinCost;
        [SerializeField] private int _maxPerNight = 1;
        [SerializeField] private float _weight = 1f;

        [Header("Execution")]
        [SerializeField] private float _arriveDistance = 1.2f;
        [SerializeField] private float _duration = 2f;

        [Header("Outcome")]
        [SerializeField] private float _damage = 1f;
        [SerializeField] private float _yinGain;
        [SerializeField] private float _villagerStress;
        [SerializeField] private bool _hasDayEffect;
        [SerializeField] private GhostDayEffect _dayEffect;
        [SerializeField] private GameObject _tracePrefab;

        public string ActionId => string.IsNullOrEmpty(_actionId) ? name : _actionId;
        public GhostActionVerb Verb => _verb;
        public GhostActionShape Shape => _verb == GhostActionVerb.None ? GhostActionShape.None : _verb == GhostActionVerb.HideTool ? GhostActionShape.Carry : GhostActionShape.Instant;
        public GhostTargetQuery Query => new GhostTargetQuery(_preferMarked, _targetKinds);
        public bool HunterOnly => _hunterOnly;
        public float MinYin => _minYin;
        public float YinCost => Mathf.Max(0f, _yinCost);
        public int MaxPerNight => _maxPerNight;
        public float Weight => Mathf.Max(0f, _weight);
        public float ArriveDistance => _arriveDistance;
        public float Duration => Mathf.Max(0f, _duration);
        public float YinGain => Mathf.Max(0f, _yinGain);
        public float VillagerStress => Mathf.Max(0f, _villagerStress);
        public float Damage => Mathf.Max(0f, _damage);
        public bool HasDayEffect => _hasDayEffect;
        public GhostDayEffect DayEffect => _dayEffect;
        public GameObject TracePrefab => _tracePrefab;
    }
}
