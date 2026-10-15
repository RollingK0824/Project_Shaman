using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerHandSway : MonoBehaviour
{
    [SerializeField] private Transform _visualPivot;
    [SerializeField] private Vector3 _screenOffset = new Vector3(-.005f, .005f, -.025f);
    [SerializeField] private Vector3 _positionAmount = new Vector3(.003f, .002f, .001f);
    [SerializeField] private Vector3 _rotationAmount = new Vector3(.3f, .25f, .35f);
    [SerializeField, Min(0)] private float _frequency = 1.7f;
    [SerializeField, Min(.1f)] private float _smooth = 10f;
    [SerializeField, Min(1)] private float _sprintMultiplier = 1.3f;
    private PlayerInputReader _input;
    private CharacterController _controller;
    private Vector3 _rest;
    private Quaternion _restRotation;
    private float _phase;
    public bool Suspended { get; set; }
    private void Awake()
    {
        _input = GetComponent<PlayerInputReader>(); _controller = GetComponent<CharacterController>();
        if (_visualPivot != null) { _rest = _visualPivot.localPosition; _restRotation = _visualPivot.localRotation; }
    }
    private void LateUpdate()
    {
        if (_visualPivot == null) return;
        bool moving = !Suspended && PlayerLocalPresentation.IsLocal(gameObject) && PlayerActionGuard.CanAct(gameObject) &&
            _input != null && _input.isActiveAndEnabled && !_input.GameplayInputBlocked && _input.MoveInput.sqrMagnitude > .01f &&
            (_controller == null || new Vector2(_controller.velocity.x, _controller.velocity.z).sqrMagnitude > .01f);
        float amount = moving ? (_input.SprintHeld ? _sprintMultiplier : 1f) : 0f;
        if (moving) _phase += Time.deltaTime * _frequency * Mathf.PI * 2f * Mathf.Sqrt(amount);
        var wave = new Vector3(Mathf.Sin(_phase), Mathf.Sin(_phase * 2f), Mathf.Cos(_phase));
        float blend = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        _visualPivot.localPosition = Vector3.Lerp(_visualPivot.localPosition, _rest + _screenOffset + Vector3.Scale(wave, _positionAmount) * amount, blend);
        _visualPivot.localRotation = Quaternion.Slerp(_visualPivot.localRotation, _restRotation * Quaternion.Euler(Vector3.Scale(wave, _rotationAmount) * amount), blend);
    }
    private void OnDisable() { if (_visualPivot != null) { _visualPivot.localPosition = _rest; _visualPivot.localRotation = _restRotation; } }
}
