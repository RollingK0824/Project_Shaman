using UnityEngine;
using UnityEngine.Audio;

// Presentation only. Remote avatars use replicated position/Animator state, never RPC audio.
[DisallowMultipleComponent, DefaultExecutionOrder(1100)]
public sealed class PlayerAudioController : MonoBehaviour
{
    [Header("Routing")]
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private AudioMixerGroup _uiGroup;
    [SerializeField] private Animator _animator;
    [Header("Ground footsteps (recordings)")]
    [SerializeField] private AudioClip[] _footsteps;
    [SerializeField, Min(.05f)] private float _walkStepInterval = .48f;
    [SerializeField, Min(.05f)] private float _sprintStepInterval = .32f;
    [SerializeField, Min(.05f)] private float _crouchStepInterval = .68f;
    [SerializeField, Range(0, 1)] private float _walkVolume = .4f;
    [SerializeField, Range(0, 1)] private float _sprintVolume = .55f;
    [SerializeField, Range(0, 1)] private float _crouchVolume = .18f;
    [SerializeField] private Vector2 _stepPitch = new Vector2(.95f, 1.05f);
    [SerializeField, Min(.01f)] private float _minimumMoveSpeed = .15f;
    [SerializeField, Min(.1f)] private float _remoteSprintSpeed = 5f;
    [Tooltip("Ignore teleports and large corrections, in metres per frame.")]
    [SerializeField, Min(.1f)] private float _teleportDistance = 2f;
    [Header("Body SFX (optional recordings)")]
    [SerializeField] private AudioClip _landing, _jump, _hurt, _death;
    [SerializeField, Min(.05f)] private float _minimumAirTime = .18f;
    [SerializeField, Min(.05f)] private float _landingCooldown = .35f;
    [Header("3D falloff")]
    [SerializeField, Min(.1f)] private float _minDistance = 1f;
    [SerializeField, Min(1f)] private float _maxDistance = 14f;
    [Header("Optional presentation clips")]
    [SerializeField] private AudioClip _projectileImpact;
    [SerializeField] private AudioClip _mirrorLive, _mirrorFreeze, _mirrorRelease;
    [SerializeField] private AudioClip _craftSuccess, _talismanSave, _talismanUse;

    private AudioSource _stepSource, _bodySource, _uiSource, _itemSource, _impactSource;
    private CharacterController _controller;
    private PlayerController _movement;
    private PlayerInputReader _input;
    private PlayerCrouch _crouch;
    private PlayerHealth _health;
    private PlayerCraftingController _craft;
    private PlayerTalismanCollection _talismans;
    private Vector3 _previousPosition;
    private float _stepTimer, _airTime, _lastLanding = -10f;
    private bool _sampled, _wasGrounded, _wasJumping, _deathPlayed;
    private int _previousStep = -1;
    private bool _hasGrounded, _hasJump, _hasCrouch;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerController>();
        _input = GetComponent<PlayerInputReader>();
        _crouch = GetComponent<PlayerCrouch>();
        _health = GetComponent<PlayerHealth>();
        _craft = GetComponent<PlayerCraftingController>();
        _talismans = GetComponent<PlayerTalismanCollection>();
        if (_animator == null) _animator = transform.Find("CharacterVisual")?.GetComponentInChildren<Animator>(true);
        if (_animator != null && _animator.runtimeAnimatorController != null)
            foreach (var p in _animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                _hasGrounded |= p.name == "Grounded";
                _hasJump |= p.name == "Jump";
                _hasCrouch |= p.name == "Crouch";
            }
        _stepSource = CreateSource("Footsteps", true);
        _bodySource = CreateSource("Body SFX", true);
        _uiSource = CreateSource("Local Item SFX", false);
    }

    private AudioSource CreateSource(string label, bool spatial)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.outputAudioMixerGroup = spatial ? _sfxGroup : _uiGroup;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = _minDistance;
        source.maxDistance = Mathf.Max(_minDistance, _maxDistance);
        return source;
    }

    private void OnEnable()
    {
        _sampled = false;
        _previousPosition = transform.position;
        if (_health != null) { _health.Damaged += OnDamaged; _health.Died += OnDied; }
        if (_craft != null) _craft.Crafted += OnCrafted;
        if (_talismans != null) { _talismans.DrawingSaved += OnSaved; _talismans.UseCommitted += OnTalismanUsed; }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 delta = transform.position - _previousPosition;
        _previousPosition = transform.position;
        if (dt <= 0f) return;
        bool local = PlayerLocalPresentation.IsLocal(gameObject);
        bool alive = _health == null || !_health.IsDead;
        bool jump = _hasJump && _animator != null && _animator.GetBool("Jump");
        bool grounded = local ? _controller != null && _controller.enabled && _controller.isGrounded :
            _hasGrounded && _animator != null && _animator.isActiveAndEnabled && _animator.GetBool("Grounded");
        grounded &= !jump;
        bool crouching = local ? _crouch != null && _crouch.IsCrouching :
            _hasCrouch && _animator != null && _animator.GetBool("Crouch");
        Vector3 velocity = local && _controller != null && _controller.enabled ? _controller.velocity : delta / dt;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        bool sprinting = local ? _input != null && _input.SprintHeld && _movement != null && _movement.CanSprint : speed >= _remoteSprintSpeed;
        bool canMove = !local || (_movement != null && _movement.CanMove && _movement.isActiveAndEnabled);
        if (!alive || !canMove || delta.sqrMagnitude > _teleportDistance * _teleportDistance)
        {
            _sampled = false; _stepTimer = 0f; _airTime = 0f; _stepSource.Stop(); return;
        }
        SampleMotion(dt, grounded, jump, speed, crouching, sprinting);
    }

    private void SampleMotion(float dt, bool grounded, bool jump, float speed, bool crouching, bool sprinting)
    {
        if (!_sampled) { _sampled = true; _wasGrounded = grounded; _wasJumping = jump; _airTime = 0f; }
        if (jump && !_wasJumping) PlayBody(_jump, .45f);
        if (!grounded) _airTime += dt;
        bool landed = grounded && !_wasGrounded && _airTime >= _minimumAirTime && Time.time - _lastLanding >= _landingCooldown;
        if (landed) { PlayBody(_landing, .55f); _lastLanding = Time.time; _stepTimer = 0f; }
        if (grounded) _airTime = 0f;
        _wasGrounded = grounded; _wasJumping = jump;
        if (!grounded || speed < _minimumMoveSpeed) { _stepTimer = 0f; _stepSource.Stop(); return; }
        float interval = crouching ? _crouchStepInterval : sprinting ? _sprintStepInterval : _walkStepInterval;
        _stepTimer += dt;
        if (_stepTimer < Mathf.Max(.05f, interval)) return;
        _stepTimer = 0f; // Never catch up multiple sounds after a hitch.
        if (_footsteps == null || _footsteps.Length == 0) return;
        int index = Random.Range(0, _footsteps.Length);
        if (_footsteps.Length > 1 && index == _previousStep) index = (index + 1) % _footsteps.Length;
        _previousStep = index;
        if (_footsteps[index] == null) return;
        _stepSource.clip = _footsteps[index];
        _stepSource.pitch = Random.Range(Mathf.Clamp(_stepPitch.x, .8f, 1.2f), Mathf.Clamp(_stepPitch.y, .8f, 1.2f));
        _stepSource.volume = crouching ? _crouchVolume : sprinting ? _sprintVolume : _walkVolume;
        _stepSource.Play();
    }

    private void PlayBody(AudioClip clip, float volume)
    {
        if (clip != null && isActiveAndEnabled) _bodySource.PlayOneShot(clip, volume);
    }
    public void PlayLocalItem(AudioClip clip, float volume = .4f)
    {
        if (clip == null || !isActiveAndEnabled || !PlayerLocalPresentation.IsLocal(gameObject)) return;
        if (_itemSource == null)
        {
            _itemSource = CreateSource("Local Use SFX", false);
            _itemSource.outputAudioMixerGroup = _sfxGroup;
        }
        _itemSource.PlayOneShot(clip, volume);
    }
    public void PlayRemoteItem(AudioClip clip, float volume = .4f)
    {
        if (PlayerLocalPresentation.IsLocal(gameObject)) return;
        PlayBody(clip, volume);
    }
    private void PlayUI(AudioClip clip)
    {
        if (clip != null && isActiveAndEnabled && PlayerLocalPresentation.IsLocal(gameObject)) _uiSource.PlayOneShot(clip, .4f);
    }
    // Called only by the visual projectile. Resolving/server projectiles stay silent.
    public void PlayProjectileImpact(Vector3 position)
    {
        if (_projectileImpact == null || !isActiveAndEnabled) return;
        if (_impactSource == null) _impactSource = CreateSource("Projectile impact", true);
        _impactSource.transform.position = position;
        _impactSource.PlayOneShot(_projectileImpact, .5f);
    }
    public void PlayMirrorLive() => PlayUI(_mirrorLive);
    public void PlayMirrorFreeze() => PlayUI(_mirrorFreeze);
    public void PlayMirrorRelease() => PlayUI(_mirrorRelease);
    private void OnCrafted(CraftingRecipeData recipe) => PlayUI(_craftSuccess);
    private void OnSaved(TalismanItemData item, TalismanDrawingData drawing) => PlayUI(_talismanSave);
    private void OnTalismanUsed(string instanceId) => PlayUI(_talismanUse);
    private void OnDamaged(float amount, GameObject source) { if (amount > 0f && _health.CurrentHealth > 0f) PlayBody(_hurt, .6f); }
    private void OnDied(GameObject source)
    {
        _stepSource.Stop(); _uiSource.Stop(); _bodySource.Stop();
        if (_itemSource != null) _itemSource.Stop();
        if (_deathPlayed) return;
        _deathPlayed = true;
        PlayBody(_death, .65f);
    }
    private void OnDisable()
    {
        if (_health != null) { _health.Damaged -= OnDamaged; _health.Died -= OnDied; }
        if (_craft != null) _craft.Crafted -= OnCrafted;
        if (_talismans != null) { _talismans.DrawingSaved -= OnSaved; _talismans.UseCommitted -= OnTalismanUsed; }
        if (_stepSource != null) _stepSource.Stop();
        if (_bodySource != null) _bodySource.Stop();
        if (_uiSource != null) _uiSource.Stop();
        if (_itemSource != null) _itemSource.Stop();
        if (_impactSource != null) _impactSource.Stop();
    }
}
