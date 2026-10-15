using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
[DefaultExecutionOrder(-20)]
public class PlayerCrouch : MonoBehaviour
{
    [SerializeField] private Transform _cameraPivot;
    [SerializeField, Min(.2f)] private float _crouchHeight = 1.1f;
    [SerializeField, Range(.1f, 1f)] private float _speedMultiplier = .5f;
    [SerializeField, Min(.1f)] private float _smooth = 12f;
    [SerializeField] private LayerMask _obstructionMask = ~0;
    private CharacterController _controller;
    private PlayerInputReader _input;
    private float _standingHeight;
    private Vector3 _standingCenter, _cameraRest;
    private readonly Collider[] _hits = new Collider[32];
    public bool IsCrouching { get; private set; }
    public float SpeedMultiplier => IsCrouching ? _speedMultiplier : 1f;
    public event Action<bool> CrouchChanged;
    private void Awake()
    {
        _controller = GetComponent<CharacterController>(); _input = GetComponent<PlayerInputReader>();
        _standingHeight = _controller.height; _standingCenter = _controller.center;
        if (_cameraPivot != null) _cameraRest = _cameraPivot.localPosition;
    }
    private void Update()
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject) || !PlayerActionGuard.CanAct(gameObject) || !_input.isActiveAndEnabled || _input.GameplayInputBlocked || !_controller.enabled) return;
        SetCrouching(_input.CrouchHeld);
        float target = IsCrouching ? Mathf.Clamp(_crouchHeight, _controller.radius * 2f, _standingHeight) : _standingHeight;
        float height = Mathf.Lerp(_controller.height, target, 1f - Mathf.Exp(-_smooth * Time.deltaTime));
        if (Mathf.Abs(height-target)<.001f) height=target;
        _controller.height = height;
        _controller.center = _standingCenter + Vector3.up * ((height - _standingHeight) * .5f);
        if (_cameraPivot != null) _cameraPivot.localPosition = _cameraRest + Vector3.up * (height - _standingHeight);
    }
    public bool SetCrouching(bool crouch)
    {
        if (!PlayerActionGuard.CanAct(gameObject) || !PlayerLocalPresentation.IsLocal(gameObject) || !_input.isActiveAndEnabled || _input.GameplayInputBlocked) return false;
        if (!crouch && !CanStand()) crouch = true;
        if (crouch != IsCrouching) { IsCrouching = crouch; CrouchChanged?.Invoke(crouch); }
        return IsCrouching == crouch;
    }
    public bool CanStand()
    {
        var scale = transform.lossyScale;
        float radius = Mathf.Max(.01f, _controller.radius * Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)) - .02f);
        float half = Mathf.Max(0, _standingHeight * Mathf.Abs(scale.y) * .5f - radius);
        var center = transform.TransformPoint(_standingCenter);
        int count = Physics.OverlapCapsuleNonAlloc(center-transform.up*half, center+transform.up*half, radius, _hits, _obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == _hits.Length) return false;
        for(int i=0;i<count;i++) if (_hits[i] != null && !_hits[i].transform.IsChildOf(transform)) return false;
        return true;
    }
}
