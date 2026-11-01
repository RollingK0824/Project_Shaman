using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Behavior;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Movement), typeof(BehaviorGraphAgent))]
    public class AI_Core : MonoBehaviour
    {
        public const string BB_CURRENT_STATE = "CurrentState";
        public const string BB_HOME_POSITION = "HomePosition";
        public const string BB_WORK_POSITION = "WorkPosition";
        public const string BB_MUST_GO_HOME = "MustGoHome";
        public const string BB_ROUTINE_SERIAL = "RoutineSerial";
        public const string BB_ROUTINE_CATEGORY = "RoutineCategory";

        [SerializeField] private AIState currentState = AIState.Idle;

        private BehaviorGraphAgent btAgent;
        private VillagerProfile _profile;
        private int _startDelayConsumedSerial = -1;
        [SerializeField] private VillagerPublicInfo _publicInfo;
        [SerializeField] private RoutineDebugView _routineDebug = new RoutineDebugView();

        public AIState CurrentState => currentState;
        public VillagerProfile Profile => _profile;
        public VillagerPublicInfo PublicInfo => _publicInfo;
        public VillagerScheduleView ScheduleView { get; private set; }
        // Today's routine blocks as clock-hour pairs (DailyBlock.ToHourPairs), for the notebook schedule on every peer
        public IReadOnlyList<int> TodayBlockHours => _todayBlockHours;
        public int TodayBlocksDay { get; private set; }
        // Clock hours: sets off home, gets in, and to bed (nightfall, or when it really fell asleep) (C131)
        public int TodayLeaveHour { get; private set; }
        public int TodayHomeHour { get; private set; }
        public int TodayBedHour { get; private set; }
        public bool IsInitialized => _profile != null;
        public ResolvedRoutine CurrentRoutine { get; private set; }
        public bool HasRoutine => CurrentRoutine.Source != null;
        public string LogId => _publicInfo != null ? _publicInfo.VillagerId : name;

        public event Action<AIState, AIState> OnStateChanged;
        public event Action<VillagerPublicInfo> OnPublicInfoApplied;
        public event Action<ResolvedRoutine> OnRoutineApplied;
        public event Action<string> OnRoutineFailed;
        public event Action<string> OnRoutineCleared;
        public event Action<SymptomType> OnSymptomTriggered;
        // Appearance symptoms (shadow, skin, eyes) active now, as a bit mask over SymptomType; raised when it changes
        public event Action<int> OnAppearanceChanged;
        public event Action<string> OnEffectTriggered;
        // Set when the villager is sent home in the evening, cleared at the next day start
        public event Action<bool> OnMustGoHomeChanged;
        public bool MustGoHome => _routineDebug.MustGoHome;
        public event System.Action OnTodayBlocksChanged;

        private int[] _todayBlockHours = Array.Empty<int>();

        public bool IsSimulating => btAgent != null && btAgent.enabled;

        void Awake()
        {
            btAgent = GetComponent<BehaviorGraphAgent>();
        }

        public void Initialize(VillagerProfile profile)
        {
            if (profile == null)
            {
                AILog.Error(AILog.CORE, $"{name} Initialize with null profile");
                return;
            }

            _profile = profile;
            _publicInfo = profile.PublicInfo;
            ScheduleView = VillagerScheduleView.FromSchedule(profile.Schedule);

            _routineDebug.SetSchedule(profile.Schedule);
            AILog.Log(AILog.CORE, LogId, $"Initialize {_publicInfo}");
            OnPublicInfoApplied?.Invoke(_publicInfo);
        }

        public void ApplyNetworkSchedule(VillagerScheduleView scheduleView)
        {
            ScheduleView = scheduleView;
            AILog.Log(AILog.NET, LogId, $"Schedule applied on client ({(scheduleView != null ? $"{scheduleView.CycleDays}x{scheduleView.SlotsPerDay}" : "none")})");
        }

        // Server: set each morning by AIManager. Client: applied from the network copy
        public void SetTodayBlocks(int dayCount, int[] blockHours, int leaveHour, int homeHour, int bedHour)
        {
            TodayBlocksDay = dayCount;
            _todayBlockHours = blockHours ?? Array.Empty<int>();
            TodayLeaveHour = leaveHour;
            TodayHomeHour = homeHour;
            TodayBedHour = bedHour;
            OnTodayBlocksChanged?.Invoke();
        }

        public void ApplyNetworkPublicInfo(VillagerPublicInfo publicInfo)
        {
            _publicInfo = publicInfo;
            AILog.Log(AILog.NET, LogId, $"Public info applied on client {_publicInfo}");
            OnPublicInfoApplied?.Invoke(_publicInfo);
        }

        public void SetState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable(BB_CURRENT_STATE, currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void ApplyNetworkState(AIState newState)
        {
            if (currentState == newState) return;
            AIState oldState = currentState;
            currentState = newState;
            
            SetBlackboardVariable(BB_CURRENT_STATE, currentState);
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void SetBehaviorGraphActive(bool active)
        {
            if (btAgent == null)
            {
                return;
            }

            if (active)
            {
                ApplyProfileToBlackboard();
            }

            btAgent.enabled = active;
            AILog.Log(AILog.CORE, LogId, $"BehaviorGraph {(active ? "enabled" : "disabled")}");
        }

        private void ApplyProfileToBlackboard()
        {
            if (_profile == null)
            {
                AILog.Warn(AILog.CORE, $"{name} BT enabled without profile, Blackboard not injected");
                return;
            }

            SetBlackboardVariable(BB_HOME_POSITION, _profile.HomePosition);
            SetBlackboardVariable(BB_MUST_GO_HOME, false);
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, RoutineCategory.None);
            AILog.Log(AILog.CORE, LogId, $"Blackboard injected Home={_profile.HomePosition}");
        }

        public void ApplyRoutine(ResolvedRoutine resolved)
        {
            CurrentRoutine = resolved;
            SetBlackboardVariable(BB_WORK_POSITION, resolved.TargetPosition);
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, resolved.Category);
            SetBlackboardVariable(BB_ROUTINE_SERIAL, resolved.Serial);
            _routineDebug.SetCurrent(resolved);
            OnRoutineApplied?.Invoke(resolved);
            AILog.Log(AILog.CORE, LogId, $"Blackboard routine {resolved}");
        }

        public void ClearRoutine(int serial, string reason)
        {
            CurrentRoutine = default;
            SetBlackboardVariable(BB_ROUTINE_CATEGORY, RoutineCategory.None);
            SetBlackboardVariable(BB_ROUTINE_SERIAL, serial);
            _routineDebug.ClearCurrent();
            _routineDebug.Serial = serial;
            AILog.Log(AILog.CORE, LogId, $"Blackboard routine cleared #{serial} ({reason})");
            OnRoutineCleared?.Invoke(reason);
        }

        // The possession stand-still already spent on the current routine (0 if none, or not yet)
        public float ConsumedStartDelaySeconds => HasRoutine && _startDelayConsumedSerial == CurrentRoutine.Serial ? CurrentRoutine.StartDelaySeconds : 0f;

        public bool TryConsumeStartDelay(out float delay)
        {
            delay = 0f;

            if (!HasRoutine || CurrentRoutine.StartDelaySeconds <= 0f || _startDelayConsumedSerial == CurrentRoutine.Serial)
            {
                return false;
            }

            _startDelayConsumedSerial = CurrentRoutine.Serial;
            delay = CurrentRoutine.StartDelaySeconds;
            return true;
        }

        public void TriggerSymptom(SymptomType symptom)
        {
            AILog.Log(AILog.GHOST, LogId, $"Symptom {symptom}");
            OnSymptomTriggered?.Invoke(symptom);
        }

        public int AppearanceMask { get; private set; }

        public bool HasAppearance(SymptomType symptom)
        {
            return (AppearanceMask & (1 << (int)symptom)) != 0;
        }

        // Server: a body change that lasts a while (not a one-off animation like TriggerSymptom)
        public void SetAppearance(SymptomType symptom, bool on)
        {
            int mask = on ? AppearanceMask | (1 << (int)symptom) : AppearanceMask & ~(1 << (int)symptom);
            if (mask == AppearanceMask)
            {
                return;
            }

            AppearanceMask = mask;
            AILog.Log(AILog.GHOST, LogId, $"Appearance {symptom} {(on ? "on" : "off")}");
            OnAppearanceChanged?.Invoke(mask);
        }

        // Client: the server's mask arrived (late joiners included)
        public void ApplyNetworkAppearance(int mask)
        {
            AppearanceMask = mask;
            OnAppearanceChanged?.Invoke(mask);
        }

        // Animator trigger on every peer for a one-off reaction outside the symptoms (startled by an item ...)
        public void TriggerEffect(string triggerName)
        {
            OnEffectTriggered?.Invoke(triggerName);
        }

        public void FailCurrentRoutine(string reason)
        {
            AILog.Log(AILog.CORE, LogId, $"Routine failed ({reason})");
            OnRoutineFailed?.Invoke(reason);
        }

        public void SetDailyStartOffset(float offsetSeconds)
        {
            _routineDebug.StartOffsetSeconds = offsetSeconds;
        }

        // Server: in the house past the bed hour, until the day starts (AIManager decides)
        public bool IsAsleep { get; private set; }
        // Time.time it fell asleep (the lie-down clip starts then), -1 while awake
        public float AsleepSince { get; private set; } = -1f;
        public event Action<bool> OnAsleepChanged;

        public void SetAsleep(bool asleep)
        {
            if (IsAsleep == asleep)
            {
                return;
            }

            IsAsleep = asleep;
            AsleepSince = asleep ? Time.time : -1f;
            // Still walking to the home point (a partial path never arrives), it got up again and walked on in the sleep pose
            GetComponent<AI_Movement>()?.SetHeldInPlace(asleep);
            AILog.Log(AILog.CORE, LogId, asleep ? "Fell asleep" : "Woke up");
            OnAsleepChanged?.Invoke(asleep);
        }

        // In the house for the evening (until the next day start); awake until IsAsleep. A player may still drop in and talk
        public bool IsHomeForNight { get; private set; }

        public void SetHomeForNight(bool atHome)
        {
            IsHomeForNight = atHome;
        }

        public void SetMustGoHome(bool mustGoHome)
        {
            SetBlackboardVariable(BB_MUST_GO_HOME, mustGoHome);
            _routineDebug.MustGoHome = mustGoHome;
            AILog.Log(AILog.CORE, LogId, $"Blackboard MustGoHome={mustGoHome}");
            OnMustGoHomeChanged?.Invoke(mustGoHome);
        }

        public void SetBlackboardVariable<T>(string variableName, T value)
        {
            if (btAgent != null && btAgent.BlackboardReference != null)
            {
                if (!btAgent.BlackboardReference.SetVariableValue(variableName, value) && _missingBlackboardWarned.Add(variableName))
                {
                    AILog.Warn(AILog.CORE, $"[{LogId}] Blackboard variable '{variableName}' ({typeof(T).Name}) not found on behavior graph, value dropped");
                }
            }
        }

        private readonly System.Collections.Generic.HashSet<string> _missingBlackboardWarned = new System.Collections.Generic.HashSet<string>();

        public bool TryGetBlackboardVariable<T>(string variableName, out T value)
        {
            if (btAgent != null && btAgent.BlackboardReference != null)
            {
                return btAgent.BlackboardReference.GetVariableValue(variableName, out value);
            }

            value = default;
            return false;
        }
    }
}
