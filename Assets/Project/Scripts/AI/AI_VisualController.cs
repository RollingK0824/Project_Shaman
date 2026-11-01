using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI
{
    // Locomotion is driven by Speed; actions, gestures and turning in place are played by state name on their own layers,
    // so the Animator needs no transitions back to locomotion. Each client plays the same calls locally (see Net_AICore)
    [RequireComponent(typeof(AI_Core), typeof(AI_Movement))]
    public class AI_VisualController : MonoBehaviour
    {
        // Same name as the player's Animator, so villager controllers can reuse its locomotion setup
        private const string MOVE_SPEED_PARAM = "Speed";
        private const string STATE_PARAM = "State";

        private const string BASE_LAYER = "Base Layer";
        private const string GESTURE_LAYER = "Gesture";
        // Full-body actions first, then the upper-body ones that keep the legs standing (hammering)
        private const string UPPER_ACTION_LAYER = "UpperAction";
        private static readonly string[] ACTION_LAYERS = { "Action", UPPER_ACTION_LAYER };
        private const string EMPTY_STATE = "Empty";
        private const string STOP_SUFFIX = " Stop";
        private const string LOCOMOTION_STATE = "Locomotion";
        private const string TURN_LEFT_STATE = "Turn Left";
        private const string TURN_RIGHT_STATE = "Turn Right";
        private const float MOVING_SPEED = 0.1f;
        // Ground speed above this with the Animator not showing locomotion is reported once (walking in an idle pose)
        private const float MISMATCH_SPEED = 0.5f;
        private const float MISMATCH_SECONDS = 1f;

        public const string TALK_GESTURE = "Talk";

        // The server sends "Symptom<Type>"; the Animator state is "Symptom_<Type>"
        private const string SYMPTOM_PREFIX = "Symptom";
        private const string SYMPTOM_STATE_PREFIX = "Symptom_";
        private static readonly string[] SYMPTOM_NAMES = { "Tremble", "StareIntoVoid", "Nonsense", "Grotesque", "NeckTwist", "WaistBend" };

        [SerializeField] private float _crossFadeSeconds = 0.2f;
        [Tooltip("Turning in place faster than this plays the turn step")]
        [SerializeField] private float _turnStartDegreesPerSecond = 30f;
        [Tooltip("Turning slower than this returns to locomotion")]
        [SerializeField] private float _turnStopDegreesPerSecond = 10f;
        [Tooltip("Share of spoken lines that come with the talk gesture, so it does not repeat on every line")]
        [SerializeField, Range(0f, 1f)] private float _talkGestureChance = 0.35f;

        [Tooltip("A symptom animation is faded out after this long, so a looping clip cannot stay on")]
        [SerializeField, Min(0.5f)] private float _symptomSeconds = 6f;

        private Coroutine _symptomRoutine;
        private Coroutine _quietSymptomRoutine;
        private float _glanceWeight;
        private float _glanceSide = 1f;
        private const float FREEZE_SECONDS = 2.5f;
        private const float GLANCE_SECONDS = 2f;
        // How far the head turns over the shoulder (neck and head together)
        private const float GLANCE_DEGREES = 75f;
        private AI_Core core;
        private AI_Movement movement;
        private Animator animator;

        private int _actionLayer = -1;
        private int _stoppingLayer = -1;
        private float _lastYaw;
        private float _yawRate;
        private int _turnDirection;
        private Vector3 _lastPosition;
        private float _mismatchSeconds;
        private bool _mismatchReported;
        private readonly HashSet<string> _reportedMissing = new HashSet<string>();

        public bool UpdateAnimatorLocally { get; set; } = true;
        // Animator state of the running action, empty when none; the server's value is copied to clients
        public string CurrentAction { get; private set; } = string.Empty;

        void Awake()
        {
            core = GetComponent<AI_Core>();
            movement = GetComponent<AI_Movement>();
            animator = GetComponentInChildren<Animator>();
            _lastYaw = transform.eulerAngles.y;
            _lastPosition = transform.position;

            core.OnStateChanged += HandleStateChanged;

            // Shadow, skin and eye changes of the possession symptoms (shown on every peer from the core's appearance mask)
            if (!TryGetComponent(out AI_SymptomVisual _))
            {
                gameObject.AddComponent<AI_SymptomVisual>();
            }

            if (TryGetComponent(out AI_VillagerModel model))
            {
                model.OnModelChanged += BindAnimator;
            }
        }

        // The body was swapped for the villager's gender before anything played on it
        private void BindAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            _actionLayer = -1;
            _stoppingLayer = -1;
            _turnDirection = 0;
            CurrentAction = string.Empty;
        }

        void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (UpdateAnimatorLocally && movement != null)
            {
                animator.SetFloat(MOVE_SPEED_PARAM, movement.CurrentSpeed);
            }

            bool moving = animator.GetFloat(MOVE_SPEED_PARAM) > MOVING_SPEED;
            UpdateStop(moving);
            UpdateTurn(moving);
            CheckWalkingPose();
        }

        public void PlayAction(string state)
        {
            state ??= string.Empty;
            if (state == CurrentAction)
            {
                return;
            }

            StopAction();
            if (state.Length == 0 || animator == null)
            {
                return;
            }

            int layer = FindActionLayer(state);
            if (layer < 0)
            {
                if (_reportedMissing.Add(state))
                {
                    AILog.Warn(AILog.WORK, $"{name}: no Animator state '{state}' in the action layers");
                }
                return;
            }

            CurrentAction = state;
            _actionLayer = layer;
            _stoppingLayer = -1;
            CrossFade(layer, state);
            AILog.Log(AILog.WORK, name, $"Animation {state} ({animator.GetLayerName(layer)})");
        }

        // Plays the action's "<state> Stop" when the Animator has one (it returns to Empty by itself), otherwise fades out
        public void StopAction()
        {
            if (CurrentAction.Length == 0)
            {
                return;
            }

            string stop = CurrentAction + STOP_SUFFIX;
            CurrentAction = string.Empty;
            if (animator == null || _actionLayer < 0)
            {
                return;
            }

            bool hasStop = HasState(_actionLayer, stop);
            CrossFade(_actionLayer, hasStop ? stop : EMPTY_STATE);
            AILog.Log(AILog.WORK, name, $"Animation end → {(hasStop ? stop : EMPTY_STATE)}");
            _stoppingLayer = hasStop ? _actionLayer : -1;
            _actionLayer = -1;
        }

        // Rolled once on the server per line, so every client shows the same lines with a gesture
        public bool RollTalkGesture()
        {
            return Random.value < _talkGestureChance;
        }

        // Upper-body gesture while standing; skipped while walking up to someone or while an action owns the arms
        public void PlayGesture(string state)
        {
            if (animator == null || !animator.isActiveAndEnabled || CurrentAction.Length > 0 ||
                animator.GetFloat(MOVE_SPEED_PARAM) > MOVING_SPEED)
            {
                return;
            }

            int layer = animator.GetLayerIndex(GESTURE_LAYER);
            if (layer >= 0 && HasState(layer, state))
            {
                CrossFade(layer, state);
            }
        }

        // A stop step is cut short once the villager walks off, so the feet do not slide
        private void UpdateStop(bool moving)
        {
            if (_stoppingLayer < 0)
            {
                return;
            }

            if (animator.GetCurrentAnimatorStateInfo(_stoppingLayer).fullPathHash == PathHash(_stoppingLayer, EMPTY_STATE))
            {
                _stoppingLayer = -1;
            }
            else if (moving)
            {
                CrossFade(_stoppingLayer, EMPTY_STATE);
                _stoppingLayer = -1;
            }
        }

        // Turning in place (facing a station or someone) steps the feet; the rotation itself comes from the AI or the network
        private void UpdateTurn(bool moving)
        {
            float yaw = transform.eulerAngles.y;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float rate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime;
            _lastYaw = yaw;
            _yawRate = Mathf.Lerp(_yawRate, rate, Mathf.Clamp01(deltaTime * 10f));

            int direction = _turnDirection;
            if (moving || Mathf.Abs(_yawRate) < _turnStopDegreesPerSecond)
            {
                direction = 0;
            }
            else if (Mathf.Abs(_yawRate) > _turnStartDegreesPerSecond)
            {
                direction = _yawRate > 0f ? 1 : -1;
            }

            // Checked every frame against where the layer really is or is heading, not only when the direction changes:
            // a fade back to Locomotion asked while the fade into a turn was still running could be dropped, and the
            // villager then ran its errands in the turning-on-the-spot pose
            _turnDirection = direction;
            int layer = animator.GetLayerIndex(BASE_LAYER);
            string state = direction > 0 ? TURN_RIGHT_STATE : direction < 0 ? TURN_LEFT_STATE : LOCOMOTION_STATE;
            if (layer < 0 || !HasState(layer, state))
            {
                return;
            }

            int hash = PathHash(layer, state);
            AnimatorStateInfo heading = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
            if (heading.fullPathHash != hash)
            {
                CrossFade(layer, state);
            }
        }

        // Measured from the transform, so it also catches a client whose synced Speed lags or a move that bypasses the agent
        private AI_Bump _bump;

        private void CheckWalkingPose()
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 step = transform.position - _lastPosition;
            _lastPosition = transform.position;
            step.y = 0f;
            float groundSpeed = step.magnitude / deltaTime;

            if (_bump == null)
            {
                _bump = GetComponent<AI_Bump>();
            }

            string pose = groundSpeed > MISMATCH_SPEED && (_bump == null || !_bump.IsDisplaced) ? DescribeWrongPose() : null;
            if (pose == null)
            {
                _mismatchSeconds = 0f;
                _mismatchReported = false;
                return;
            }

            _mismatchSeconds += deltaTime;
            if (!_mismatchReported && _mismatchSeconds >= MISMATCH_SECONDS)
            {
                _mismatchReported = true;
                AILog.Warn(AILog.WORK, $"{name} moving {groundSpeed:F1}m/s without walk animation: {pose}");
            }
        }

        private string DescribeWrongPose()
        {
            float speed = animator.GetFloat(MOVE_SPEED_PARAM);
            int baseLayer = animator.GetLayerIndex(BASE_LAYER);
            bool baseWalking = baseLayer < 0 || animator.GetCurrentAnimatorStateInfo(baseLayer).fullPathHash == PathHash(baseLayer, LOCOMOTION_STATE);
            string covering = null;
            foreach (string layerName in ACTION_LAYERS)
            {
                // The upper-body layer is masked to the arms and head: a symptom there leaves the legs walking
                if (layerName == UPPER_ACTION_LAYER)
                {
                    continue;
                }

                int layer = animator.GetLayerIndex(layerName);
                if (layer >= 0 && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != PathHash(layer, EMPTY_STATE))
                {
                    covering = layerName;
                }
            }

            if (speed > MOVING_SPEED && baseWalking && covering == null)
            {
                return null;
            }

            return $"Speed={speed:F2}, base={(baseWalking ? LOCOMOTION_STATE : "not locomotion")}, covered by={covering ?? "none"}, action='{CurrentAction}', " +
                $"local={UpdateAnimatorLocally}, agent={(movement != null ? movement.DescribeAgent() : "none")}";
        }

        private int FindActionLayer(string state)
        {
            foreach (string layerName in ACTION_LAYERS)
            {
                int layer = animator.GetLayerIndex(layerName);
                if (layer >= 0 && HasState(layer, state))
                {
                    return layer;
                }
            }

            return -1;
        }

        private void CrossFade(int layer, string state)
        {
            // Work can end while the villager is being destroyed with its Animator already off
            if (animator.isActiveAndEnabled)
            {
                animator.CrossFadeInFixedTime(PathHash(layer, state), _crossFadeSeconds, layer);
            }
        }

        private bool HasState(int layer, string state)
        {
            return animator.HasState(layer, PathHash(layer, state));
        }

        // States sit directly under their layer, so "<layer>.<state>" is their full path
        private int PathHash(int layer, string state)
        {
            return Animator.StringToHash(animator.GetLayerName(layer) + "." + state);
        }

        private void HandleStateChanged(AIState oldState, AIState newState)
        {
            SetIntegerIfExists(STATE_PARAM, (int)newState);
        }

        public Animator Animator => animator;

        public bool HasParameter(string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetIntegerIfExists(string parameterName, int value)
        {
            if (HasParameter(parameterName))
            {
                animator.SetInteger(parameterName, value);
            }
        }

        public void TriggerActionEffect(string triggerName)
        {
            if (triggerName != null && triggerName.StartsWith(SYMPTOM_PREFIX))
            {
                PlaySymptom(triggerName.Substring(SYMPTOM_PREFIX.Length));
                return;
            }

            if (HasParameter(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        // A behaviour symptom: the Animator state "Symptom_<Name>" (Symptom_Tremble, Symptom_NeckTwist ...) on the first action
        // layer that has it (the upper-body layer, so walking goes on). It is not an action (work keeps its own state) and is
        // faded out when the clip ends (capped for a looping clip). A state the Animator lacks is reported once.
        public void PlaySymptom(string symptomName)
        {
            if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrEmpty(symptomName))
            {
                return;
            }

            // The quiet ones are done in code on every peer, no clip
            if (symptomName == nameof(SymptomType.Freeze))
            {
                StartQuietSymptom(FreezeRoutine());
                return;
            }

            if (symptomName == nameof(SymptomType.GlanceBack))
            {
                StartQuietSymptom(GlanceBackRoutine());
                return;
            }

            string state = SYMPTOM_STATE_PREFIX + symptomName;
            int layer = FindActionLayer(state);
            if (layer < 0)
            {
                if (_reportedMissing.Add(state))
                {
                    AILog.Warn(AILog.GHOST, $"{name}: no Animator state '{state}' in the action layers (symptom has no animation yet)");
                }

                return;
            }

            // Only a layer that is idle: a symptom never cuts off a work animation on the same layer
            if (animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != PathHash(layer, EMPTY_STATE))
            {
                return;
            }

            CrossFade(layer, state);
            if (_symptomRoutine != null)
            {
                StopCoroutine(_symptomRoutine);
            }

            _symptomRoutine = StartCoroutine(EndSymptom(layer, _symptomSeconds));
        }

        private void StartQuietSymptom(System.Collections.IEnumerator routine)
        {
            if (_quietSymptomRoutine != null)
            {
                return;
            }

            _quietSymptomRoutine = StartCoroutine(RunQuietSymptom(routine));
        }

        private System.Collections.IEnumerator RunQuietSymptom(System.Collections.IEnumerator routine)
        {
            yield return routine;
            _quietSymptomRoutine = null;
        }

        // Freeze: the whole body stops mid-motion for a moment, like a puppet whose strings went still, then goes on
        private System.Collections.IEnumerator FreezeRoutine()
        {
            float speed = animator.speed;
            animator.speed = 0f;
            yield return new WaitForSeconds(FREEZE_SECONDS);
            if (animator != null)
            {
                animator.speed = speed;
            }
        }

        // GlanceBack: the head turns over the shoulder as if someone were behind, holds, and turns back (LateUpdate poses it)
        private System.Collections.IEnumerator GlanceBackRoutine()
        {
            _glanceSide = Random.value < 0.5f ? -1f : 1f;
            for (float t = 0f; t < GLANCE_SECONDS; t += Time.deltaTime)
            {
                // Up in the first quarter, held, back in the last quarter
                float k = t / GLANCE_SECONDS;
                _glanceWeight = Mathf.SmoothStep(0f, 1f, Mathf.Min(k * 4f, (1f - k) * 4f, 1f));
                yield return null;
            }

            _glanceWeight = 0f;
        }

        private void LateUpdate()
        {
            if (_glanceWeight <= 0f || animator == null || !animator.isHuman)
            {
                return;
            }

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            float yaw = GLANCE_DEGREES * _glanceWeight * _glanceSide;
            if (neck != null)
            {
                neck.rotation = Quaternion.AngleAxis(yaw * 0.4f, transform.up) * neck.rotation;
            }

            if (head != null)
            {
                head.rotation = Quaternion.AngleAxis(yaw * 0.6f, transform.up) * head.rotation;
            }
        }

        // Back to Empty when the clip has played once (the symptom clips do not loop), or after the cap for a looping one
        private System.Collections.IEnumerator EndSymptom(int layer, float maxSeconds)
        {
            float giveUpAt = Time.time + maxSeconds;

            // The cross-fade reaches the symptom state only after it has blended in
            yield return new WaitForSeconds(_crossFadeSeconds + 0.05f);
            while (Time.time < giveUpAt && animator != null && animator.isActiveAndEnabled && StateIsSymptom(layer) &&
                   animator.GetCurrentAnimatorStateInfo(layer).normalizedTime < 1f)
            {
                yield return null;
            }

            _symptomRoutine = null;

            // Cut only our own state: work may have taken the layer meanwhile
            if (animator != null && animator.isActiveAndEnabled && StateIsSymptom(layer))
            {
                CrossFade(layer, EMPTY_STATE);
            }
        }

        private bool StateIsSymptom(int layer)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            foreach (string suffix in SYMPTOM_NAMES)
            {
                if (info.fullPathHash == PathHash(layer, SYMPTOM_STATE_PREFIX + suffix))
                {
                    return true;
                }
            }

            return false;
        }

        void OnDestroy()
        {
            if (core != null)
            {
                core.OnStateChanged -= HandleStateChanged;
            }

            if (TryGetComponent(out AI_VillagerModel model))
            {
                model.OnModelChanged -= BindAnimator;
            }
        }
    }
}
