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
    [Header("Local breathing")]
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
    [Tooltip("Shared recorded sounds. Disable only to use this prefab's legacy clip fields.")]
    [SerializeField] private bool _useRecordedSounds = true;
    private PlayerSoundBank _sounds;
    public bool UsesRecordedSounds => _sounds != null;
    private readonly System.Collections.Generic.Dictionary<PlayerSoundCue, int> _lastSounds = new();
    private AudioSource _drawingSource;
    private float _noticeAt = -10f, _drawingAt = -10f;
    private bool _noticePending;
    private AudioClip[] _woodFootsteps;

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
        ApplyRecordedSounds();
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

    private void ApplyRecordedSounds()
    {
        _sounds = _useRecordedSounds ? PlayerSoundBank.Default : null;
        if (_sounds == null) return;
        bool female = _sounds.IsFemale(_animator);
        _sfxGroup = _sounds.SfxGroup; _uiGroup = _sounds.UiGroup;
        _footsteps = _sounds.Clips(PlayerSoundCue.DirtStep);
        _woodFootsteps = _sounds.Clips(PlayerSoundCue.WoodStep);
        _landing = _sounds.First(PlayerSoundCue.Landing);
        _breathingClip = _sounds.First(female ? PlayerSoundCue.FemaleBreath : PlayerSoundCue.MaleBreath);
        _breathingVolume = _sounds.Volume(female ? PlayerSoundCue.FemaleBreath : PlayerSoundCue.MaleBreath);
        _heartbeatClip = _sounds.First(PlayerSoundCue.Heartbeat);
        _heartbeatVolume = new Vector2(_sounds.Volume(PlayerSoundCue.Heartbeat) * .4f, _sounds.Volume(PlayerSoundCue.Heartbeat));
        _hurt = _sounds.First(female ? PlayerSoundCue.FemaleHurt : PlayerSoundCue.MaleHurt);
        _death = _sounds.First(female ? PlayerSoundCue.FemaleDeath : PlayerSoundCue.MaleDeath);
        _projectileImpact = _sounds.First(PlayerSoundCue.Impact);
        _mirrorLive = _sounds.First(PlayerSoundCue.MirrorLive);
        _mirrorFreeze = _mirrorRelease = _sounds.First(PlayerSoundCue.MirrorToggle);
        _craftSuccess = _talismanSave = _sounds.First(PlayerSoundCue.Notice);
        _talismanUse = _sounds.First(PlayerSoundCue.Talisman);
    }

    public void PlayCue(PlayerSoundCue cue, bool remote = false)
    {
        if (_sounds == null || !isActiveAndEnabled || (NetworkServer.active && !NetworkClient.active)) return;
        if (!_lastSounds.TryGetValue(cue, out int previous)) previous = -1;
        var clip = _sounds.Pick(cue, ref previous);
        _lastSounds[cue] = previous;
        if (remote) PlayRemoteItem(clip, _sounds.Volume(cue));
        else if (cue == PlayerSoundCue.Notebook || cue == PlayerSoundCue.Notice || cue == PlayerSoundCue.Talisman ||
                 cue == PlayerSoundCue.MirrorLive || cue == PlayerSoundCue.MirrorToggle)
        {
            if (clip != null && PlayerLocalPresentation.IsLocal(gameObject)) _uiSource.PlayOneShot(clip, _sounds.Volume(cue));
        }
        else PlayLocalItem(clip, _sounds.Volume(cue));
    }

    // Inventory changes and Crafted can fire in the same transaction/frame.
    // Coalesce them into one local notification, after the transaction finishes.
    public void QueueAcquisitionSound() => _noticePending = true;
    public void PlayDrawing()
    {
        if (_sounds == null || !isActiveAndEnabled || !PlayerLocalPresentation.IsLocal(gameObject) ||
            !PlayerActionGuard.CanAct(gameObject) || Time.unscaledTime < _drawingAt) return;
        var clip = _sounds.First(PlayerSoundCue.Drawing);
        if (clip == null) return;
        if (_drawingSource == null) _drawingSource = CreateSource("Local Drawing", false);
        _drawingSource.clip = clip; _drawingSource.volume = _sounds.Volume(PlayerSoundCue.Drawing);
        _drawingSource.Play(); _drawingAt = Time.unscaledTime + clip.length;
    }
    public void StopDrawing()
    {
        if (_drawingSource != null) _drawingSource.Stop();
        _drawingAt = -10f;
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
        if (_noticePending)
        {
            _noticePending = false;
            if (Time.unscaledTime - _noticeAt >= .15f && PlayerActionGuard.CanAct(gameObject))
            { if (_sounds != null) PlayCue(PlayerSoundCue.Notice); else PlayUI(_craftSuccess); _noticeAt = Time.unscaledTime; }
        }
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
        if (!canMove || teleport)
        {
            _sampled = false; _stepTimer = 0f; _airTime = 0f; _stepSource.Stop(); return;
        }
        SampleMotion(dt, grounded, jump, moved ? speed : 0f, crouching, sprinting);
    }

    private void SampleMotion(float dt, bool grounded, bool jump, float speed, bool crouching, bool sprinting)
    {
        if (!_sampled) { _sampled = true; _wasGrounded = grounded; _wasJumping = jump; _airTime = 0f; }
        if (jump && !_wasJumping) PlayBody(_jump, .45f);
        if (!grounded) _airTime += dt;
        bool landed = grounded && !_wasGrounded && _airTime >= _minimumAirTime && Time.time - _lastLanding >= _landingCooldown;
        if (landed) { PlayBody(_landing, _sounds != null ? _sounds.Volume(PlayerSoundCue.Landing) : .55f); _lastLanding = Time.time; _stepTimer = 0f; }
        if (grounded) _airTime = 0f;
        _wasGrounded = grounded; _wasJumping = jump;
        if (!grounded || speed < _minimumMoveSpeed) { _stepTimer = 0f; _stepSource.Stop(); return; }
        float interval = crouching ? _crouchStepInterval : sprinting ? _sprintStepInterval : _walkStepInterval;
        _stepTimer += dt;
        if (_stepTimer < Mathf.Max(.05f, interval)) return;
        _stepTimer = 0f; // Never catch up multiple sounds after a hitch.
        var steps = GroundClips();
        if (steps == null || steps.Length == 0) return;
        int index = Random.Range(0, steps.Length);
        if (steps.Length > 1 && index == _previousStep) index = (index + 1) % steps.Length;
        _previousStep = index;
        if (steps[index] == null) return;
        _stepSource.clip = steps[index];
        _stepSource.pitch = Random.Range(Mathf.Clamp(_stepPitch.x, .8f, 1.2f), Mathf.Clamp(_stepPitch.y, .8f, 1.2f));
        float volume = crouching ? _crouchVolume : sprinting ? _sprintVolume : _walkVolume;
        if (_sounds != null) volume *= _sounds.Volume(ReferenceEquals(steps, _woodFootsteps) ? PlayerSoundCue.WoodStep : PlayerSoundCue.DirtStep) / .4f;
        _stepSource.volume = Mathf.Clamp01(volume * Random.Range(1f - _stepVolumeVariation, 1f + _stepVolumeVariation));
        _stepSource.PlayOneShot(steps[index]);
    }

    private AudioClip[] GroundClips()
    {
        // An explicit Player-owned surface marker avoids guessing wood from object names.
        Vector3 feet = _controller != null ? _controller.bounds.center - Vector3.up * _controller.bounds.extents.y : transform.position;
        if (_woodFootsteps != null && Physics.Raycast(feet + Vector3.up * .15f, Vector3.down, out var hit, .4f, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<PlayerFootstepSurface>() is { Wood: true })
            return _woodFootsteps;
        return _footsteps;
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
        StopDrawing();
        _noticePending = false;
        if (_stepSource != null) _stepSource.Stop();
        // Death voice finishes after entering spectator; loops and footsteps stop immediately.
        if (_bodySource != null && !_deathPlayed) _bodySource.Stop();
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
        _impactSource.PlayOneShot(_projectileImpact, _sounds != null ? _sounds.Volume(PlayerSoundCue.Impact) : .5f);
    }
    public void PlayMirrorLive() { if (_sounds != null) PlayCue(PlayerSoundCue.MirrorLive); else PlayUI(_mirrorLive); }
    public void PlayMirrorFreeze() { if (_sounds != null) PlayCue(PlayerSoundCue.MirrorToggle); else PlayUI(_mirrorFreeze); }
    public void PlayMirrorRelease() { if (_sounds != null) PlayCue(PlayerSoundCue.MirrorToggle); else PlayUI(_mirrorRelease); }
    private void OnCrafted(CraftingRecipeData recipe) => QueueAcquisitionSound();
    private void OnSaved(TalismanItemData item, TalismanDrawingData drawing) => QueueAcquisitionSound();
    private void OnTalismanUsed(string instanceId) { if (_sounds != null) PlayCue(PlayerSoundCue.Talisman); else PlayUI(_talismanUse); }
    private void OnDamaged(float amount, GameObject source)
    {
        if (amount > 0f && _health.CurrentHealth > 0f)
            PlayBody(_hurt, _sounds != null ? _sounds.Volume(_sounds.IsFemale(_animator) ? PlayerSoundCue.FemaleHurt : PlayerSoundCue.MaleHurt) : .6f);
    }
    private void OnDied(GameObject source)
    {
        StopLocalBody();
        _stepSource.Stop(); _uiSource.Stop(); _bodySource.Stop();
        if (_itemSource != null) _itemSource.Stop();
        if (_deathPlayed) return;
        _deathPlayed = true;
        PlayBody(_death, _sounds != null ? _sounds.Volume(_sounds.IsFemale(_animator) ? PlayerSoundCue.FemaleDeath : PlayerSoundCue.MaleDeath) : .65f);
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
