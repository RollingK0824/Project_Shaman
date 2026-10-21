using UnityEngine;
using UnityEngine.Audio;
using Mirror;

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
    [SerializeField, Range(0f, .15f)] private float _stepVolumeVariation = .05f;
    [SerializeField, Min(.01f)] private float _minimumMoveSpeed = .15f;
    [SerializeField, Min(.1f)] private float _remoteSprintSpeed = 5f;
    [Tooltip("Ignore teleports and large corrections, in metres per frame.")]
    [SerializeField, Min(.1f)] private float _teleportDistance = 2f;
    [Header("Local breathing (synthetic placeholder; replace with recording)")]
    [SerializeField] private AudioClip _breathingClip;
    [SerializeField, Min(.1f)] private float _minimumSprintDuration = 3f;
    [SerializeField, Min(.1f)] private float _recoveryDuration = 4f;
    [SerializeField, Min(.05f)] private float _breathingFadeSeconds = .6f;
    [SerializeField, Range(0f, 1f)] private float _breathingVolume = .22f;
    [SerializeField] private Vector2 _breathingPitch = new Vector2(1f, 1.22f);
    [Header("Local heartbeat (one pulse pair per beat)")]
    [SerializeField] private AudioClip _heartbeatClip;
    [Tooltip("-1 follows PlayerStressController.HighThreshold. Overrides cannot enable heartbeat below High.")]
    [SerializeField, Range(-1f, 100f)] private float _heartbeatStartThreshold = -1f;
    [SerializeField] private Vector2 _heartbeatBpm = new Vector2(65f, 115f);
    [SerializeField] private Vector2 _heartbeatVolume = new Vector2(.12f, .3f);
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
    private PlayerStressController _stress;
    private PlayerSpectatorController _spectator;
    private Transform _characterVisual;
    private AudioSource _breathingSource, _heartbeatSource;
    private float _sprintTime, _recoveryRemaining, _breathingIntensity, _heartbeatTimer;
    private bool _heartbeatActive;
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
        _stress = GetComponent<PlayerStressController>();
        _spectator = GetComponent<PlayerSpectatorController>();
        _characterVisual = transform.Find("CharacterVisual");
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
        var feedback = GetComponent<PlayerFeedbackController>();
        _breathingSource = feedback != null ? feedback.BreathingSource : null;
        _heartbeatSource = feedback != null ? feedback.HeartbeatSource : null;
        if (_breathingSource == null) _breathingSource = CreateSource("Local Breathing", false);
        if (_heartbeatSource == null) _heartbeatSource = CreateSource("Local Heartbeat", false);
        ConfigureBodySource(_breathingSource, _breathingClip, true);
        ConfigureBodySource(_heartbeatSource, _heartbeatClip, false);
    }

    private void ConfigureBodySource(AudioSource source, AudioClip clip, bool loop)
    {
        source.Stop();
        if (clip != null) source.clip = clip; // Existing Network prefabs may still use the prior clips.
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
        source.volume = 0f;
        source.outputAudioMixerGroup = _sfxGroup;
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
        _deathPlayed = false;
        if (_spectator != null) _spectator.SpectatorModeEntered += StopPlayerSounds;
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
        bool alive = (_health == null || !_health.IsDead) && (_spectator == null || !_spectator.IsSpectating);
        if (!alive || (_characterVisual != null && !_characterVisual.gameObject.activeInHierarchy) ||
            (NetworkServer.active && !NetworkClient.active))
        {
            _sampled = false; _stepTimer = 0f; _airTime = 0f;
            StopPlayerSounds();
            return;
        }
        bool jump = _hasJump && _animator != null && _animator.GetBool("Jump");
        bool grounded = local ? _controller != null && _controller.enabled && _controller.isGrounded :
            _hasGrounded && _animator != null && _animator.isActiveAndEnabled && _animator.GetBool("Grounded");
        grounded &= !jump;
        bool crouching = local ? _crouch != null && _crouch.IsCrouching :
            _hasCrouch && _animator != null && _animator.GetBool("Crouch");
        Vector3 velocity = local && _controller != null && _controller.enabled ? _controller.velocity : delta / dt;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        bool sprinting = !crouching && (local ? _input != null && _input.isActiveAndEnabled &&
            !_input.GameplayInputBlocked && _input.SprintHeld && _movement != null && _movement.CanSprint : speed >= _remoteSprintSpeed);
        bool canMove = !local || (_movement != null && _movement.CanMove && _movement.isActiveAndEnabled);
        bool teleport = delta.sqrMagnitude > _teleportDistance * _teleportDistance;
        // Displacement also guards against a stale CharacterController.velocity when Move was skipped.
        bool moved = new Vector2(delta.x, delta.z).magnitude / dt >= _minimumMoveSpeed;
        UpdateLocalBody(dt, local, canMove && !teleport && grounded && moved && speed >= _minimumMoveSpeed && sprinting);
        if (!canMove || teleport || !moved)
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
        float volume = crouching ? _crouchVolume : sprinting ? _sprintVolume : _walkVolume;
        _stepSource.volume = Mathf.Clamp01(volume * Random.Range(1f - _stepVolumeVariation, 1f + _stepVolumeVariation));
        _stepSource.PlayOneShot(_footsteps[index]);
    }

    private void UpdateLocalBody(float dt, bool local, bool running)
    {
        if (!local) { StopLocalBody(); return; }
        if (running)
        {
            _sprintTime += dt;
            if (_sprintTime >= _minimumSprintDuration) _recoveryRemaining = _recoveryDuration;
        }
        else _sprintTime = 0f;
        if (!running || _sprintTime < _minimumSprintDuration)
            _recoveryRemaining = Mathf.Max(0f, _recoveryRemaining - dt);
        float fatigue = _recoveryRemaining > 0f ? .65f * Mathf.Clamp01(_recoveryRemaining / Mathf.Max(.1f, _recoveryDuration)) : 0f;
        bool high = _stress != null && _stress.CurrentStress >= _stress.HighThreshold;
        float stressIntensity = high ? Mathf.Lerp(.4f, 1f, _stress.HighIntensity) : 0f;
        float target = Mathf.Max(fatigue, stressIntensity);
        _breathingIntensity = Mathf.MoveTowards(_breathingIntensity, target, dt / Mathf.Max(.05f, _breathingFadeSeconds));
        _breathingSource.volume = Mathf.Clamp01(_breathingVolume * _breathingIntensity);
        _breathingSource.pitch = Mathf.Lerp(Mathf.Clamp(_breathingPitch.x, .8f, 1.4f), Mathf.Clamp(_breathingPitch.y, .8f, 1.4f), _breathingIntensity);
        if (_breathingIntensity > .001f && _breathingSource.clip != null)
        {
            if (!_breathingSource.isPlaying) _breathingSource.Play();
        }
        else { _breathingSource.Stop(); _breathingSource.volume = 0f; }

        float threshold = _stress != null ? Mathf.Max(_stress.HighThreshold, _heartbeatStartThreshold) : 100f;
        if (!high || _stress.CurrentStress < threshold || _heartbeatSource.clip == null)
        {
            _heartbeatSource.Stop(); _heartbeatSource.volume = 0f;
            _heartbeatActive = false; _heartbeatTimer = 0f;
            return;
        }
        float heartIntensity = Mathf.InverseLerp(threshold, _stress.MaxStress, _stress.CurrentStress);
        float bpm = Mathf.Lerp(Mathf.Max(1f, _heartbeatBpm.x), Mathf.Max(_heartbeatBpm.x, _heartbeatBpm.y), heartIntensity);
        _heartbeatSource.volume = Mathf.Clamp01(Mathf.Lerp(_heartbeatVolume.x, _heartbeatVolume.y, heartIntensity));
        _heartbeatTimer -= dt;
        if (!_heartbeatActive || _heartbeatTimer <= 0f)
        {
            _heartbeatSource.PlayOneShot(_heartbeatSource.clip);
            // Long legacy clips cannot overlap themselves; new short clips allow the full BPM range.
            _heartbeatTimer = Mathf.Max(60f / bpm, _heartbeatSource.clip.length);
            _heartbeatActive = true;
        }
    }

    private void StopLocalBody()
    {
        _sprintTime = _recoveryRemaining = _breathingIntensity = _heartbeatTimer = 0f;
        _heartbeatActive = false;
        if (_breathingSource != null) { _breathingSource.Stop(); _breathingSource.volume = 0f; }
        if (_heartbeatSource != null) { _heartbeatSource.Stop(); _heartbeatSource.volume = 0f; }
    }

    private void StopPlayerSounds()
    {
        StopLocalBody();
        if (_stepSource != null) _stepSource.Stop();
        if (_bodySource != null) _bodySource.Stop();
        if (_uiSource != null) _uiSource.Stop();
        if (_itemSource != null) _itemSource.Stop();
        if (_impactSource != null) _impactSource.Stop();
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
        StopLocalBody();
        _stepSource.Stop(); _uiSource.Stop(); _bodySource.Stop();
        if (_itemSource != null) _itemSource.Stop();
        if (_deathPlayed) return;
        _deathPlayed = true;
        PlayBody(_death, .65f);
    }
    private void OnDisable()
    {
        if (_spectator != null) _spectator.SpectatorModeEntered -= StopPlayerSounds;
        StopPlayerSounds();
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
