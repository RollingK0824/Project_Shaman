using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth), typeof(PlayerInputReader))]
public class PlayerSpectatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [Tooltip("Optional world bounds. Leave empty for unrestricted free flight.")]
    [SerializeField] private Collider _flightBounds;

    [Header("Free Camera")]
    [SerializeField, Min(0f)] private float _moveSpeed = 6f;
    [SerializeField, Min(1f)] private float _fastMultiplier = 2f;
    [SerializeField, Min(0f)] private float _lookSensitivity = 0.08f;

    [Header("Follow Camera")]
    [SerializeField] private Vector3 _followOffset = new Vector3(0.5f, 2f, -3f);
    [SerializeField, Min(0f)] private float _followBlendSpeed = 10f;

    public bool IsSpectating { get; private set; }
    public bool IsFollowing => _target != null;
    public GameObject SpectateTarget => _target != null ? _target.gameObject : null;
    public event Action SpectatorModeEntered;
    public event Action SpectatorModeExited;
    public event Action<GameObject> SpectateTargetChanged;
    public event Action NoLivingTargets;

    private PlayerHealth _health;
    private PlayerInputReader _input;
    private NetworkIdentity _identity;
    private PlayerViewModeController _view;
    private PlayerFeedbackController _feedback;
    private PlayerHealth _target;
    private readonly List<PlayerHealth> _targets = new List<PlayerHealth>();
    private Camera _spectatorCamera;
    private AudioListener _playerListener;
    private bool _cameraWasEnabled;
    private bool _listenerWasEnabled;
    public Camera SpectatorCamera => _spectatorCamera;
    private bool _viewWasEnabled;
    private bool _feedbackWasEnabled;
    private bool _noTargets;
    private float _yaw;
    private float _pitch;

    private bool IsLocalView => (!NetworkClient.active && !NetworkServer.active) ||
        (_identity != null && _identity.isLocalPlayer);

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _input = GetComponent<PlayerInputReader>();
        _identity = GetComponent<NetworkIdentity>();
        _view = GetComponent<PlayerViewModeController>();
        _feedback = GetComponent<PlayerFeedbackController>();
        if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>(true);
    }

    private void OnEnable()
    {
        _health.Died += HandleDied;
        PlayerHealth.PlayersChanged += RefreshTargets;
        _input.PreviousSpectateTargetPressed += PreviousTarget;
        _input.NextSpectateTargetPressed += NextTarget;
        _input.ToggleSpectateModePressed += ToggleFollow;
    }

    private void OnDisable()
    {
        _health.Died -= HandleDied;
        PlayerHealth.PlayersChanged -= RefreshTargets;
        _input.PreviousSpectateTargetPressed -= PreviousTarget;
        _input.NextSpectateTargetPressed -= NextTarget;
        _input.ToggleSpectateModePressed -= ToggleFollow;
        ExitSpectatorMode();
    }

    private void HandleDied(GameObject source) => EnterSpectatorMode();

    public void EnterSpectatorMode()
    {
        if (IsSpectating || !_health.IsDead || !IsLocalView || _playerCamera == null) return;
        _viewWasEnabled = _view != null && _view.enabled;
        _feedbackWasEnabled = _feedback != null && _feedback.enabled;
        if (_view != null) { _view.EndCurrentMode(); _view.enabled = false; }
        if (_feedback != null) _feedback.enabled = false;
        _cameraWasEnabled = _playerCamera.enabled;
        _playerListener = _playerCamera.GetComponent<AudioListener>();
        _listenerWasEnabled = _playerListener != null && _playerListener.enabled;
        var cameraObject = new GameObject("Local Spectator Camera");
        cameraObject.transform.SetPositionAndRotation(_playerCamera.transform.position, _playerCamera.transform.rotation);
        _spectatorCamera = cameraObject.AddComponent<Camera>();
        _spectatorCamera.CopyFrom(_playerCamera);
        _spectatorCamera.enabled = true;
        if (_listenerWasEnabled) cameraObject.AddComponent<AudioListener>();
        _playerCamera.enabled = false;
        if (_playerListener != null) _playerListener.enabled = false;
        ReadCameraAngles();
        _noTargets = false;
        IsSpectating = true;
        SpectatorModeEntered?.Invoke();
        RefreshTargets();
    }

    public void ExitSpectatorMode()
    {
        if (!IsSpectating) return;
        SetTarget(null);
        IsSpectating = false;
        if (_spectatorCamera != null)
        {
            _spectatorCamera.gameObject.SetActive(false);
            Destroy(_spectatorCamera.gameObject);
            _spectatorCamera = null;
        }
        bool restoreLocalView = IsLocalView && gameObject.activeInHierarchy && _input.isActiveAndEnabled;
        if (restoreLocalView)
        {
            if (_playerCamera != null) _playerCamera.enabled = _cameraWasEnabled;
            if (_playerListener != null) _playerListener.enabled = _listenerWasEnabled;
        }
        if (restoreLocalView && _view != null) _view.enabled = _viewWasEnabled;
        if (restoreLocalView && _feedback != null) _feedback.enabled = _feedbackWasEnabled;
        SpectatorModeExited?.Invoke();
    }

    private void LateUpdate()
    {
        if (!IsLocalView) return;
        // Also covers a late-joining owner whose replicated death predates OnEnable.
        if (!IsSpectating && _health.IsDead) EnterSpectatorMode();
        if (!IsSpectating || _spectatorCamera == null || _input.GameplayInputBlocked) return;
        if (_target != null)
        {
            if (!_target.isActiveAndEnabled || _target.IsDead) { RefreshTargets(); return; }
            Vector3 position = _target.transform.TransformPoint(_followOffset);
            var cameraTransform = _spectatorCamera.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, ClampPosition(position),
                1f - Mathf.Exp(-_followBlendSpeed * Time.unscaledDeltaTime));
            Vector3 direction = _target.transform.position + Vector3.up * 1.5f - cameraTransform.position;
            if (direction.sqrMagnitude > 0.001f) cameraTransform.rotation = Quaternion.LookRotation(direction);
            return;
        }

        Vector2 look = _input.LookInput;
        _yaw += look.x * _lookSensitivity;
        _pitch = Mathf.Clamp(_pitch - look.y * _lookSensitivity, -85f, 85f);
        var camera = _spectatorCamera.transform;
        camera.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector2 move = _input.MoveInput;
        Vector3 directionFree = camera.right * move.x + camera.forward * move.y + Vector3.up * _input.SpectatorVerticalInput;
        float speed = _moveSpeed * (_input.SprintHeld ? _fastMultiplier : 1f);
        camera.position = ClampPosition(camera.position + Vector3.ClampMagnitude(directionFree, 1f) * speed * Time.unscaledDeltaTime);
    }

    private Vector3 ClampPosition(Vector3 position) => _flightBounds == null ? position : _flightBounds.bounds.ClosestPoint(position);

    private void ReadCameraAngles()
    {
        _yaw = _spectatorCamera.transform.eulerAngles.y;
        _pitch = Mathf.DeltaAngle(0f, _spectatorCamera.transform.eulerAngles.x);
    }

    private void RefreshTargets()
    {
        if (!IsSpectating || !IsLocalView) return;
        int previousIndex = _targets.IndexOf(_target);
        bool wasFollowing = !ReferenceEquals(_target, null);
        _targets.Clear();
        foreach (var player in PlayerHealth.ActivePlayers)
        {
            if (player == null || player == _health || !player.isActiveAndEnabled || player.IsDead) continue;
            if (NetworkClient.active && (!player.TryGetComponent<NetworkIdentity>(out var identity) || !identity.isClient)) continue;
            _targets.Add(player);
        }
        if (wasFollowing && !_targets.Contains(_target))
            SetTarget(_targets.Count == 0 ? null : _targets[Mathf.Clamp(previousIndex, 0, _targets.Count - 1)]);
        bool empty = _targets.Count == 0;
        if (empty && !_noTargets) NoLivingTargets?.Invoke();
        _noTargets = empty;
    }

    public void NextTarget() => CycleTarget(1);
    public void PreviousTarget() => CycleTarget(-1);
    private void CycleTarget(int direction)
    {
        if (!IsSpectating || !IsLocalView) return;
        RefreshTargets();
        if (_targets.Count == 0) { SetTarget(null); return; }
        int index = _targets.IndexOf(_target);
        int next = index < 0 ? (direction > 0 ? 0 : _targets.Count - 1) :
            (index + direction + _targets.Count) % _targets.Count;
        SetTarget(_targets[next]);
    }

    public void UseFreeCamera()
    {
        if (!IsSpectating || !IsLocalView) return;
        SetTarget(null);
    }
    public void ToggleFollow()
    {
        if (IsFollowing) UseFreeCamera();
        else NextTarget();
    }

    private void SetTarget(PlayerHealth target)
    {
        if (ReferenceEquals(_target, target)) return;
        _target = target;
        if (_spectatorCamera != null) ReadCameraAngles();
        SpectateTargetChanged?.Invoke(SpectateTarget);
    }
}
