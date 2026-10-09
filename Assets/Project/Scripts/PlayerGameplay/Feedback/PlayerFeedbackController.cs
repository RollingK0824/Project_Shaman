using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _feedbackPivot;
    [SerializeField] private Volume _hitVolume;
    [SerializeField] private Camera _feedbackCamera;
    [SerializeField] private Transform _handFeedbackPivot;
    [SerializeField] private Volume _stressVolume;
    [SerializeField] private AudioSource _heartbeatSource;
    [SerializeField] private AudioSource _breathingSource;

    [Header("Hit Shake")]
    [SerializeField] private float _hitShakeDuration = 0.2f;
    [SerializeField] private float _hitShakeAmount = 0.04f;
    [SerializeField] private float _hitRotationAmount = 1.5f;
    [SerializeField] private float _hitShakeSpeed = 30f;

    [Header("Stress Shake")]
    [SerializeField] private float _maxStressShakeAmount = 0.025f;
    [SerializeField] private float _stressShakeSpeed = 14f;
    [SerializeField, Min(0f)] private float _maxStressRotationAmount = 0.8f;
    [SerializeField, Min(0f)] private float _maxHandShakeAmount = 0.003f;
    [SerializeField, Min(0f)] private float _maxHandRotationAmount = 1.2f;
    [SerializeField, Min(0.01f)] private float _stressBlendSpeed = 2f;
    [Header("Stress Audio")]
    [SerializeField, Range(0f, 1f)] private float _heartbeatVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float _breathingVolume = 0.22f;
    [SerializeField] private Vector2 _heartbeatPitch = new Vector2(1f, 1.6f);
    [SerializeField] private Vector2 _breathingPitch = new Vector2(1f, 1.5f);

    [Header("Post Processing")]
    [SerializeField] private float _volumeFadeSpeed = 6f;

    private PlayerHealth _playerHealth;
    private PlayerStressController _stressController;

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private float _hitShakeTimer;
    private float _hitVolumeWeight;
    private Vector3 _handBasePosition;
    private Quaternion _handBaseRotation;
    private AudioListener _feedbackListener;
    private float _stressIntensity;
    private float _stressVolumeWeight;

    public float StressFeedbackIntensity => _stressIntensity;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _stressController = GetComponent<PlayerStressController>();
        if (_feedbackCamera == null) _feedbackCamera = GetComponentInChildren<Camera>(true);
        if (_feedbackCamera != null) _feedbackListener = _feedbackCamera.GetComponent<AudioListener>();
        if (_handFeedbackPivot != null)
        {
            _handBasePosition = _handFeedbackPivot.localPosition;
            _handBaseRotation = _handFeedbackPivot.localRotation;
        }
        ConfigureLoop(_heartbeatSource);
        ConfigureLoop(_breathingSource);
        if (_stressVolume != null) _stressVolume.weight = 0f;

        if (_feedbackPivot != null)
        {
            _baseLocalPosition = _feedbackPivot.localPosition;
            _baseLocalRotation = _feedbackPivot.localRotation;
        }

        if (_hitVolume != null)
        {
            _hitVolume.weight = 0f;
        }
    }

    private void OnEnable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.Damaged += HandleDamaged;
        }
    }

    private void LateUpdate()
    {
        // Presentation follows the active view, without depending on Mirror or
        // writing camera-look, locomotion, health, or item animation state.
        bool activeView = (_feedbackCamera == null || _feedbackCamera.isActiveAndEnabled) &&
            (_feedbackListener == null || _feedbackListener.isActiveAndEnabled);
        if (!activeView)
        {
            ResetFeedback();
            return;
        }
        UpdateStressFeedback();
        UpdateCameraFeedback();
        UpdateHitVolume();
    }

    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.Damaged -= HandleDamaged;
        }

        ResetFeedback();
    }

    private void HandleDamaged(float damageAmount, GameObject source)
    {
        _hitShakeTimer = _hitShakeDuration;
        _hitVolumeWeight = 1f;

        string sourceName = source != null ? source.name : "Unknown";
        Debug.Log(
            $"[Feedback] 피격 / Damage = {damageAmount:0.##} / Source = {sourceName}",
            gameObject
        );
    }

    private void UpdateCameraFeedback()
    {
        if (_feedbackPivot == null)
        {
            return;
        }

        Vector3 stressOffset = GetStressOffset();
        Vector3 hitOffset = Vector3.zero;
        Quaternion hitRotation = Quaternion.identity;

        if (_hitShakeTimer > 0f)
        {
            _hitShakeTimer -= Time.unscaledDeltaTime;

            float normalized = _hitShakeDuration <= 0f
                ? 0f
                : Mathf.Clamp01(_hitShakeTimer / _hitShakeDuration);

            float strength = normalized * normalized;
            float time = Time.unscaledTime * _hitShakeSpeed;

            float noiseX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
            float noiseRotation = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f;

            hitOffset = new Vector3(noiseX, noiseY, 0f) * _hitShakeAmount * strength;
            hitRotation = Quaternion.Euler(
                0f,
                0f,
                noiseRotation * _hitRotationAmount * strength
            );
        }

        _feedbackPivot.localPosition = _baseLocalPosition + stressOffset + hitOffset;
        float roll = (Mathf.PerlinNoise(Time.time * _stressShakeSpeed, 7.3f) - 0.5f) * 2f;
        _feedbackPivot.localRotation = _baseLocalRotation * hitRotation *
            Quaternion.Euler(0f, 0f, roll * _maxStressRotationAmount * _stressIntensity);
    }

    private Vector3 GetStressOffset()
    {
        if (_stressController == null)
        {
            return Vector3.zero;
        }

        float intensity = _stressIntensity;

        if (intensity <= 0f)
        {
            return Vector3.zero;
        }

        float time = Time.time * _stressShakeSpeed;
        float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;

        return new Vector3(offsetX, offsetY, 0f) * _maxStressShakeAmount * intensity;
    }

    private void UpdateStressFeedback()
    {
        float target = _stressController != null ? _stressController.FeedbackIntensity : 0f;
        _stressIntensity = Mathf.MoveTowards(_stressIntensity, target, _stressBlendSpeed * Time.deltaTime);
        float high = _stressController != null ? _stressController.HighIntensity : 0f;
        _stressVolumeWeight = Mathf.MoveTowards(_stressVolumeWeight, high, _stressBlendSpeed * Time.deltaTime);
        if (_stressVolume != null) _stressVolume.weight = _stressVolumeWeight;

        UpdateLoop(_heartbeatSource, _heartbeatVolume, _heartbeatPitch);
        UpdateLoop(_breathingSource, _breathingVolume, _breathingPitch);
        if (_handFeedbackPivot == null) return;
        float time = Time.time * _stressShakeSpeed;
        Vector3 noise = new Vector3(
            (Mathf.PerlinNoise(time, 19f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(31f, time) - 0.5f) * 2f,
            (Mathf.PerlinNoise(time, 43f) - 0.5f) * 2f);
        _handFeedbackPivot.localPosition = _handBasePosition + noise * _maxHandShakeAmount * _stressIntensity;
        _handFeedbackPivot.localRotation = _handBaseRotation *
            Quaternion.Euler(noise * _maxHandRotationAmount * _stressIntensity);
    }

    private static void ConfigureLoop(AudioSource source)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void UpdateLoop(AudioSource source, float volume, Vector2 pitch)
    {
        if (source == null || source.clip == null) return;
        source.volume = Mathf.Clamp01(volume * _stressIntensity);
        source.pitch = Mathf.Lerp(pitch.x, pitch.y, _stressIntensity);
        if (_stressIntensity > 0.001f)
        {
            if (!source.isPlaying) source.Play();
        }
        else if (source.isPlaying) source.Stop();
    }

    private void UpdateHitVolume()
    {
        if (_hitVolume == null)
        {
            return;
        }

        _hitVolumeWeight = Mathf.MoveTowards(
            _hitVolumeWeight,
            0f,
            _volumeFadeSpeed * Time.unscaledDeltaTime
        );

        _hitVolume.weight = _hitVolumeWeight;
    }

    private void ResetFeedback()
    {
        _hitShakeTimer = 0f;
        _hitVolumeWeight = 0f;
        _stressIntensity = 0f;
        _stressVolumeWeight = 0f;
        if (_stressVolume != null) _stressVolume.weight = 0f;
        StopLoop(_heartbeatSource);
        StopLoop(_breathingSource);
        if (_handFeedbackPivot != null)
        {
            _handFeedbackPivot.localPosition = _handBasePosition;
            _handFeedbackPivot.localRotation = _handBaseRotation;
        }

        if (_feedbackPivot != null)
        {
            _feedbackPivot.localPosition = _baseLocalPosition;
            _feedbackPivot.localRotation = _baseLocalRotation;
        }

        if (_hitVolume != null)
        {
            _hitVolume.weight = 0f;
        }
    }

    private static void StopLoop(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }
}
