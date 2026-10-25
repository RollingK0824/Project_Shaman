using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "AIBehaviourConfig", menuName = "ProjectShaman/AI/Behaviour Config")]
    public class AIBehaviourConfig : ScriptableObject
    {
        [SerializeField] private int _cycleDays = 3;
        [SerializeField, Range(0f, 1f)] private float _restRatio = 0.3f;
        [SerializeField] private int _routinesPerDay = 2;
        [Tooltip("Share of the day one routine block takes (2 blocks x 0.4 + 0.2 spare = day 4:4:2)")]
        [SerializeField, Range(0.05f, 1f)] private float _routineDayRatio = 0.4f;
        [Tooltip("Shortest a block may shrink to when the walk home does not fit the spare, as a share of the full block")]
        [SerializeField, Range(0.05f, 1f)] private float _minRoutineRatio = 0.5f;
        [SerializeField] private int _maxStartDelayHours = 2;
        [Tooltip("Every block but the last is randomly up to this share shorter per villager, so villagers do not all switch routines at the same moment")]
        [SerializeField, Range(0f, 0.5f)] private float _blockLengthVariance = 0.35f;
        [Tooltip("A block with less than this share of its length left is skipped instead of started")]
        [SerializeField, Range(0f, 1f)] private float _minRemainingRatio = 0.5f;
        [SerializeField] private float _homeReturnMarginSeconds = 10f;
        [Tooltip("Clock hours one of which each villager must be home by (half-sijin rows on the schedule tab). The walk home is planned back from it")]
        [SerializeField] private int[] _homeArriveHours = { 18, 19 };
        [Tooltip("Clock hours one of which each villager goes to bed (night starts at 20). Awake villagers cannot be sleep-paralysed")]
        [SerializeField] private int[] _bedHours = { 20, 21 };
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
        [SerializeField] private float _maxYin = 100f;
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtZeroYin = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtMaxYin = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _lastSlotChanceMultiplier = 0.5f;
        [SerializeField] private int _minCorruptionsPerDay = 1;
        [SerializeField, Range(0f, 1f)] private float _timeDelayMinRatio = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _timeDelayMaxRatio = 0.25f;
        [SerializeField] private int _maxSymptomsPerRestSlot = 3;
        [SerializeField, Range(0f, 1f)] private float _workSlotSymptomMultiplier = 0.5f;
        [SerializeField] private float _emptyHandToolYinThreshold = 80f;
        [SerializeField, Range(0f, 1f)] private float _emptyHandToolChance = 0.25f;
        [SerializeField] private float _toolCorruptionSearchRadius = 25f;

        [Header("Night Hunt")]
        [SerializeField] private List<HuntTier> _huntTiers = new List<HuntTier>
        {
            new HuntTier(1f, 1, false),
            new HuntTier(26f, 2, false),
            new HuntTier(51f, 3, false),
            new HuntTier(76f, 4, true)
        };
        [SerializeField] private float _ghostHuntTimeoutSeconds = 90f;
        [SerializeField] private float _huntRadiusMin = 10f;
        [SerializeField] private float _huntRadiusMax = 60f;
        [SerializeField] private float _ghostSpawnDelaySeconds = 3f;

        [Header("Stress")]
        [SerializeField] private float _toolMissingStress = 5f;
        [SerializeField] private float _toolSearchFailedStress = 10f;

        [Header("Night Action")]
        [SerializeField] private float _nightWitnessRadius = 15f;
        [SerializeField] private float _sleepParalysisHomeDistance = 4f;
        [SerializeField] private float _ghostHideMinDistance = 8f;
        [SerializeField] private float _ghostHideMaxDistance = 20f;
        [SerializeField] private float _ghostActionCooldownMin = 15f;
        [SerializeField] private float _ghostActionCooldownMax = 30f;
        [SerializeField] private float _ghostCommandRetrySeconds = 1f;
        [SerializeField] private float _ghostCommandTimeoutSeconds = 30f;
        [SerializeField] private float _ghostMinChannelSeconds = 1.5f;

        [Header("Ghost Channel Items")]
        [SerializeField] private float _ghostSaltRetreatSeconds = 8f;
        [SerializeField, Range(0f, 1f)] private float _ghostBeanRewindRatio = 0.3f;
        [SerializeField] private float _ghostBellRevealSeconds = 6f;

        [Header("Ghost Idle")]
        [SerializeField] private float _ghostStalkMinDistance = 3f;
        [SerializeField] private float _ghostStalkMaxDistance = 6f;
        [SerializeField] private float _ghostStalkSpeed = 3f;
        [SerializeField] private float _ghostStalkCreepSpeed = 1f;
        [SerializeField] private float _ghostStalkStressPerSecond = 0.5f;
        [SerializeField] private float _ghostWanderSpeed = 1.5f;
        [SerializeField] private float _ghostWanderPauseMin = 2f;
        [SerializeField] private float _ghostWanderPauseMax = 5f;

        [Header("Ghost Player Chase")]
        [SerializeField] private float _ghostPlayerDetectDistance = 15f;
        [SerializeField] private float _ghostPlayerAttackCooldown = 10f;

        [Header("Interaction")]
        [SerializeField] private float _interactionNoticeRadius = 10f;
        [SerializeField] private float _interactionScanInterval = 0.5f;
        [SerializeField] private float _glanceDuration = 1.5f;
        [SerializeField, Range(0, 100)] private int _restResistance = 20;
        [SerializeField, Range(0, 100)] private int _conversationResistance = 30;
        [SerializeField, Range(0, 100)] private int _movingResistance = 40;
        [SerializeField, Range(0, 100)] private int _toolSearchResistance = 50;
        [SerializeField, Range(0, 100)] private int _workResistance = 60;
        [SerializeField, Range(0, 100)] private int _returningHomeResistance = 70;

        [Header("Conversation")]
        [SerializeField] private float _conversationRadius = 5f;
        [SerializeField] private float _conversationCheckInterval = 2f;
        [SerializeField, Range(0f, 1f)] private float _conversationChance = 0.3f;
        [SerializeField] private int _conversationsPerRoutine = 2;
        [SerializeField] private float _conversationDurationMin = 8f;
        [SerializeField] private float _conversationDurationMax = 12f;
        [SerializeField] private int _conversationGroupMax = 4;
        [SerializeField] private float _conversationPartnerCooldown = 60f;
        [SerializeField, Min(0f)] private float _hearingDistance = 12f;

        [Header("Ghost Trace")]
        [SerializeField, Min(0f)] private float _traceNeglectStressPerDay = 0.5f;
        [SerializeField, Min(1f)] private float _traceNeglectStressMax = 3f;

        [Header("Memory")]
        [SerializeField, Min(1)] private int _memoryDays = 2;
        [SerializeField, Min(1)] private int _memoryEntriesPerKind = 10;

        [Header("Villager Stress")]
        [SerializeField, Min(1f)] private float _villagerStressMax = 100f;
        [SerializeField, Min(0f)] private float _villagerStressAnxious = 50f;
        [SerializeField, Min(0f)] private float _villagerStressLimit = 100f;
        [SerializeField, Min(0f)] private float _villagerStressMorningDecay = 5f;

        [Header("Player Item Reaction")]
        [Tooltip("Stress a villager takes when salt, bean-mugwort, the bell or the gong reaches them")]
        [SerializeField, Min(0f)] private float _itemStartleStress = 3f;
        [Tooltip("After reacting to an item a villager ignores that same item for this long, so spamming a cooldown-free item cannot pile on stress or show a host at will")]
        [SerializeField] private ItemReactionCooldowns _villagerItemCooldowns = new ItemReactionCooldowns(6f, 6f, 6f, 6f);
        [Tooltip("Host yin from which the ghost shows through the body (trembling); below it the host is startled like anyone")]
        [SerializeField, Min(0f)] private float _itemReactionYin = 40f;
        [Tooltip("Host yin from which the reaction turns violent (grotesque twisting, lashing out)")]
        [SerializeField, Min(0f)] private float _itemViolentYin = 70f;

        [Header("Vision")]
        [SerializeField, Range(10f, 180f)] private float _visionHalfAngle = 60f;
        [SerializeField, Min(0f)] private float _visionNearRadius = 2f;
        [SerializeField, Min(0f)] private float _visionEyeHeight = 1.6f;
        [Tooltip("Buildings, walls, terrain. Empty = no line-of-sight test")]
        [SerializeField] private LayerMask _visionObstacleMask = 0;

        [Header("Observation")]
        [SerializeField, Min(1f)] private float _observationRadius = 10f;
        [SerializeField, Min(0.1f)] private float _observationInterval = 1f;
        [SerializeField, Range(1, 3)] private int _witnessTraitMin = 1;
        [SerializeField, Range(1, 3)] private int _witnessTraitMax = 2;
        [SerializeField, Min(5f)] private float _anxiousMutterInterval = 30f;
        [SerializeField, Range(0f, 1f)] private float _anxiousMutterChance = 0.35f;

        [Tooltip("How long a neighbour stays at a house it looks in on, on the way home")]
        [SerializeField, Min(0f)] private float _neighbourVisitLingerSeconds = 3f;

        [Header("Dialogue")]
        [SerializeField, Range(0f, 1f)] private float _rumorChance = 0.5f;
        [SerializeField, Range(1, 4)] private int _rumorMaxHops = 2;
        [SerializeField, Min(1)] private int _playerQuestionsPerDay = 2;

        [Header("Villager Request")]
        [SerializeField] private List<RequestQuestEntry> _requestQuests = new List<RequestQuestEntry>
        {
            new RequestQuestEntry("FindTool", "q_find_tool"),
            new RequestQuestEntry("Trace", "q_clear_trace"),
            new RequestQuestEntry("TraceCleanse", "q_cleanse_trace"),
            new RequestQuestEntry("CheckOn", "q_check_on"),
            new RequestQuestEntry("Gut", "q_gut")
        };
        [SerializeField, Min(0f)] private float _requestCompletedStressRelief = 30f;
        [SerializeField, Min(0f)] private float _requestFailedStress = 10f;

        [Header("Debug")]
        [SerializeField, Tooltip("Temporary: possessed villagers wear the debug material on every peer")]
        private bool _showPossessedMaterial = true;
        [SerializeField] private Material _possessedDebugMaterial;

        public int CycleDays => _cycleDays;
        public float RestRatio => _restRatio;
        public int RoutinesPerDay => Mathf.Max(1, _routinesPerDay);
        public float RoutineSeconds => TimeManager.DAY_DURATION * _routineDayRatio;
        public float MinRoutineSeconds => RoutineSeconds * _minRoutineRatio;
        public float MaxStartDelaySeconds => DayClock.HoursToSeconds(Mathf.Max(0, _maxStartDelayHours));
        public float BlockLengthVariance => _blockLengthVariance;
        public float MinRemainingRatio => _minRemainingRatio;
        public float HomeReturnMarginSeconds => _homeReturnMarginSeconds;
        public IReadOnlyList<int> HomeArriveHours => _homeArriveHours;
        public IReadOnlyList<int> BedHours => _bedHours;
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
        public float MaxYin => Mathf.Max(_minimumYin, _maxYin);
        public float CorruptionChanceAtZeroYin => _corruptionChanceAtZeroYin;
        public float CorruptionChanceAtMaxYin => _corruptionChanceAtMaxYin;
        public float LastSlotChanceMultiplier => _lastSlotChanceMultiplier;
        public int MinCorruptionsPerDay => _minCorruptionsPerDay;
        public float TimeDelayMinRatio => _timeDelayMinRatio;
        public float TimeDelayMaxRatio => _timeDelayMaxRatio;
        public int MaxSymptomsPerRestSlot => _maxSymptomsPerRestSlot;
        public float WorkSlotSymptomMultiplier => _workSlotSymptomMultiplier;
        public float EmptyHandToolYinThreshold => _emptyHandToolYinThreshold;
        public float EmptyHandToolChance => _emptyHandToolChance;
        public float ToolCorruptionSearchRadius => _toolCorruptionSearchRadius;
        public IReadOnlyList<HuntTier> HuntTiers => _huntTiers;
        public float GhostHuntTimeoutSeconds => Mathf.Max(_ghostCommandTimeoutSeconds, _ghostHuntTimeoutSeconds);
        public float HuntRadiusMin => _huntRadiusMin;
        public float HuntRadiusMax => _huntRadiusMax;
        public float GhostSpawnDelaySeconds => _ghostSpawnDelaySeconds;
        public float ToolMissingStress => _toolMissingStress;
        public float ToolSearchFailedStress => _toolSearchFailedStress;
        public float NightWitnessRadius => _nightWitnessRadius;
        public float SleepParalysisHomeDistance => _sleepParalysisHomeDistance;
        public float GhostHideMinDistance => _ghostHideMinDistance;
        public float GhostHideMaxDistance => Mathf.Max(_ghostHideMinDistance, _ghostHideMaxDistance);
        public float GhostActionCooldownMin => Mathf.Max(0f, _ghostActionCooldownMin);
        public float GhostActionCooldownMax => Mathf.Max(GhostActionCooldownMin, _ghostActionCooldownMax);
        public float GhostCommandRetrySeconds => _ghostCommandRetrySeconds;
        public float GhostCommandTimeoutSeconds => _ghostCommandTimeoutSeconds;
        public float GhostMinChannelSeconds => Mathf.Max(0f, _ghostMinChannelSeconds);
        public float GhostSaltRetreatSeconds => Mathf.Max(0f, _ghostSaltRetreatSeconds);
        public float GhostBeanRewindRatio => Mathf.Clamp01(_ghostBeanRewindRatio);
        public float GhostBellRevealSeconds => Mathf.Max(0f, _ghostBellRevealSeconds);
        public float GhostStalkMinDistance => Mathf.Max(0f, _ghostStalkMinDistance);
        public float GhostStalkMaxDistance => Mathf.Max(GhostStalkMinDistance, _ghostStalkMaxDistance);
        public float GhostStalkSpeed => Mathf.Max(0.1f, _ghostStalkSpeed);
        public float GhostStalkCreepSpeed => Mathf.Max(0.1f, _ghostStalkCreepSpeed);
        public float GhostStalkStressPerSecond => Mathf.Max(0f, _ghostStalkStressPerSecond);
        public float GhostWanderSpeed => Mathf.Max(0.1f, _ghostWanderSpeed);
        public float GhostWanderPauseMin => Mathf.Max(0f, _ghostWanderPauseMin);
        public float GhostWanderPauseMax => Mathf.Max(GhostWanderPauseMin, _ghostWanderPauseMax);
        public float GhostPlayerDetectDistance => Mathf.Max(0f, _ghostPlayerDetectDistance);
        public float GhostPlayerAttackCooldown => Mathf.Max(0f, _ghostPlayerAttackCooldown);
        public float InteractionNoticeRadius => Mathf.Max(0f, _interactionNoticeRadius);
        public float InteractionScanInterval => Mathf.Max(0.05f, _interactionScanInterval);
        public float GlanceDuration => Mathf.Max(0f, _glanceDuration);
        public float ConversationRadius => Mathf.Max(0f, _conversationRadius);
        public float ConversationCheckInterval => Mathf.Max(0.1f, _conversationCheckInterval);
        public float ConversationChance => _conversationChance;
        public int ConversationsPerRoutine => Mathf.Max(0, _conversationsPerRoutine);
        public float ConversationDurationMin => Mathf.Max(0f, _conversationDurationMin);
        public float ConversationDurationMax => Mathf.Max(ConversationDurationMin, _conversationDurationMax);
        public int ConversationGroupMax => Mathf.Max(2, _conversationGroupMax);
        public float ConversationPartnerCooldown => Mathf.Max(0f, _conversationPartnerCooldown);
        public float HearingDistance => Mathf.Max(0f, _hearingDistance);
        public float TraceNeglectStressPerDay => Mathf.Max(0f, _traceNeglectStressPerDay);
        public float TraceNeglectStressMax => Mathf.Max(1f, _traceNeglectStressMax);
        public int MemoryDays => Mathf.Max(1, _memoryDays);
        public int MemoryEntriesPerKind => Mathf.Max(1, _memoryEntriesPerKind);
        public float VillagerStressMax => Mathf.Max(1f, _villagerStressMax);
        public float VillagerStressLimit => Mathf.Clamp(_villagerStressLimit, 0f, VillagerStressMax);
        public float VillagerStressAnxious => Mathf.Clamp(_villagerStressAnxious, 0f, VillagerStressLimit);
        public float VillagerStressMorningDecay => Mathf.Max(0f, _villagerStressMorningDecay);
        public ItemReactionCooldowns VillagerItemCooldowns => _villagerItemCooldowns;
        public float ItemStartleStress => Mathf.Max(0f, _itemStartleStress);
        public float ItemReactionYin => Mathf.Max(0f, _itemReactionYin);
        public float ItemViolentYin => Mathf.Max(ItemReactionYin, _itemViolentYin);
        public float VisionHalfAngle => _visionHalfAngle;
        public float VisionNearRadius => _visionNearRadius;
        public float VisionEyeHeight => _visionEyeHeight;
        public LayerMask VisionObstacleMask => _visionObstacleMask;
        public float ObservationRadius => _observationRadius;
        public float ObservationInterval => _observationInterval;
        public int WitnessTraitMin => Mathf.Clamp(_witnessTraitMin, 1, 3);
        public int WitnessTraitMax => Mathf.Clamp(_witnessTraitMax, WitnessTraitMin, 3);
        public float AnxiousMutterInterval => _anxiousMutterInterval;
        public float AnxiousMutterChance => _anxiousMutterChance;
        public float NeighbourVisitLingerSeconds => _neighbourVisitLingerSeconds;
        public float RumorChance => _rumorChance;
        public int RumorMaxHops => _rumorMaxHops;
        public int PlayerQuestionsPerDay => _playerQuestionsPerDay;
        public float RequestCompletedStressRelief => _requestCompletedStressRelief;
        public float RequestFailedStress => _requestFailedStress;
        public bool ShowPossessedMaterial => _showPossessedMaterial && _possessedDebugMaterial != null;
        public Material PossessedDebugMaterial => _possessedDebugMaterial;

        // "Kind:Detail" wins over "Kind", so one tool or trace kind can get its own quest
        public bool TryGetRequestQuest(string kind, string detail, out string questId)
        {
            questId = null;

            if (_requestQuests == null || string.IsNullOrEmpty(kind))
            {
                return false;
            }

            string exactKey = string.IsNullOrEmpty(detail) ? null : $"{kind}:{detail}";

            foreach (RequestQuestEntry entry in _requestQuests)
            {
                if (string.IsNullOrEmpty(entry.QuestId))
                {
                    continue;
                }

                if (entry.RequestKey == exactKey)
                {
                    questId = entry.QuestId;
                    return true;
                }

                if (entry.RequestKey == kind && questId == null)
                {
                    questId = entry.QuestId;
                }
            }

            return questId != null;
        }

        public int GetInterruptResistance(VillagerActivity activity)
        {
            switch (activity)
            {
                case VillagerActivity.Rest:
                    return _restResistance;
                case VillagerActivity.Conversation:
                    return _conversationResistance;
                case VillagerActivity.Moving:
                    return _movingResistance;
                case VillagerActivity.ToolSearch:
                    return _toolSearchResistance;
                case VillagerActivity.Work:
                    return _workResistance;
                case VillagerActivity.ReturningHome:
                    return _returningHomeResistance;
                default:
                    return _workResistance;
            }
        }

        public bool TryGetHuntTier(float yin, out HuntTier tier)
        {
            tier = default;
            bool found = false;

            if (_huntTiers == null)
            {
                return false;
            }

            foreach (HuntTier candidate in _huntTiers)
            {
                if (candidate.Kills > 0 && yin >= candidate.MinYin && (!found || candidate.MinYin > tier.MinYin))
                {
                    tier = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
