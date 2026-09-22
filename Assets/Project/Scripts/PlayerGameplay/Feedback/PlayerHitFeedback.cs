using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _hitFeedbackPivot;
    [SerializeField] private Volume _hitVolume;

    [Header("Camera Shake")]
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _shakeAmount = 0.04f;
    [SerializeField] private float _shakeRotationAmount = 1.5f;
    [SerializeField] private float _shakeSpeed = 30f;

    [Header("Post Processing")]
    [SerializeField] private float _volumeFadeSpeed = 6f;

    private PlayerHealth _playerHealth;

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;

    private float _shakeTimer;
    private float _hitVolumeWeight;

    private void Awake()
    {
        _playerHealth =
            GetComponent<PlayerHealth>();

        if (_hitFeedbackPivot != null)
        {
            _baseLocalPosition =
                _hitFeedbackPivot.localPosition;

            _baseLocalRotation =
                _hitFeedbackPivot.localRotation;
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
        UpdateCameraShake();
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

    private void HandleDamaged(
        float damageAmount,
        GameObject source)
    {
        _shakeTimer = _shakeDuration;
        _hitVolumeWeight = 1f;

        string sourceName =
            source != null
                ? source.name
                : "Unknown";

        Debug.Log(
            $"[HitFeedback] 피격 연출 / " +
            $"Damage = {damageAmount:0.##} / " +
            $"Source = {sourceName}",
            gameObject
        );
    }

    private void UpdateCameraShake()
    {
        if (_hitFeedbackPivot == null)
        {
            return;
        }

        if (_shakeTimer <= 0f)
        {
            _hitFeedbackPivot.localPosition =
                _baseLocalPosition;

            _hitFeedbackPivot.localRotation =
                _baseLocalRotation;

            return;
        }

        _shakeTimer -=
            Time.unscaledDeltaTime;

        float normalized =
            _shakeDuration <= 0f
                ? 0f
                : Mathf.Clamp01(
                    _shakeTimer /
                    _shakeDuration
                );

        float strength =
            normalized * normalized;

        float time =
            Time.unscaledTime *
            _shakeSpeed;

        float noiseX =
            (Mathf.PerlinNoise(
                time,
                0f
            ) - 0.5f) * 2f;

        float noiseY =
            (Mathf.PerlinNoise(
                0f,
                time
            ) - 0.5f) * 2f;

        float noiseRotation =
            (Mathf.PerlinNoise(
                time,
                time
            ) - 0.5f) * 2f;

        Vector3 positionOffset =
            new Vector3(
                noiseX,
                noiseY,
                0f
            ) *
            _shakeAmount *
            strength;

        Quaternion rotationOffset =
            Quaternion.Euler(
                0f,
                0f,
                noiseRotation *
                _shakeRotationAmount *
                strength
            );

        _hitFeedbackPivot.localPosition =
            _baseLocalPosition +
            positionOffset;

        _hitFeedbackPivot.localRotation =
            _baseLocalRotation *
            rotationOffset;
    }

    private void UpdateHitVolume()
    {
        if (_hitVolume == null)
        {
            return;
        }

        _hitVolumeWeight =
            Mathf.MoveTowards(
                _hitVolumeWeight,
                0f,
                _volumeFadeSpeed *
                Time.unscaledDeltaTime
            );

        _hitVolume.weight =
            _hitVolumeWeight;
    }

    private void ResetFeedback()
    {
        _shakeTimer = 0f;
        _hitVolumeWeight = 0f;

        if (_hitFeedbackPivot != null)
        {
            _hitFeedbackPivot.localPosition =
                _baseLocalPosition;

            _hitFeedbackPivot.localRotation =
                _baseLocalRotation;
        }

        if (_hitVolume != null)
        {
            _hitVolume.weight = 0f;
        }
    }
}