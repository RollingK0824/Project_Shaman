using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _feedbackPivot;
    [SerializeField] private Volume _hitVolume;

    [Header("Hit Shake")]
    [SerializeField] private float _hitShakeDuration = 0.2f;
    [SerializeField] private float _hitShakeAmount = 0.04f;
    [SerializeField] private float _hitRotationAmount = 1.5f;
    [SerializeField] private float _hitShakeSpeed = 30f;

    [Header("Stress Shake")]
    [SerializeField] [Range(0f, 1f)] private float _stressShakeStart = 0.25f;
    [SerializeField] private float _maxStressShakeAmount = 0.025f;
    [SerializeField] private float _stressShakeSpeed = 14f;

    [Header("Post Processing")]
    [SerializeField] private float _volumeFadeSpeed = 6f;

    private PlayerHealth _playerHealth;
    private PlayerStressController _stressController;

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;
    private float _hitShakeTimer;
    private float _hitVolumeWeight;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _stressController = GetComponent<PlayerStressController>();

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
        _feedbackPivot.localRotation = _baseLocalRotation * hitRotation;
    }

    private Vector3 GetStressOffset()
    {
        if (_stressController == null)
        {
            return Vector3.zero;
        }

        float intensity = Mathf.InverseLerp(
            _stressShakeStart,
            1f,
            _stressController.NormalizedStress
        );

        if (intensity <= 0f)
        {
            return Vector3.zero;
        }

        float time = Time.time * _stressShakeSpeed;
        float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;

        return new Vector3(offsetX, offsetY, 0f) * _maxStressShakeAmount * intensity;
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
}
