using UnityEngine;

[RequireComponent(typeof(PlayerStressController))]
public class PlayerStressFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;

    [Header("Camera Shake")]
    [SerializeField] private float _maxShakeAmount = 0.025f;
    [SerializeField] private float _shakeSpeed = 14f;

    private PlayerStressController _stressController;

    private Vector3 _baseLocalPosition;

    private void Awake()
    {
        _stressController =
            GetComponent<PlayerStressController>();

        if (_playerCamera != null)
        {
            _baseLocalPosition =
                _playerCamera.transform.localPosition;
        }
    }

    private void LateUpdate()
    {
        if (_playerCamera == null ||
            _stressController == null)
        {
            return;
        }

        ApplyCameraShake();
    }

    private void OnDisable()
    {
        ResetCameraPosition();
    }

    private void ApplyCameraShake()
    {
        float stress =
            _stressController.NormalizedStress;

        // 낮은 스트레스에서는 흔들림을 거의 느끼지 않도록 한다.
        float intensity = Mathf.InverseLerp(
            0.25f,
            1f,
            stress
        );

        if (intensity <= 0f)
        {
            _playerCamera.transform.localPosition =
                _baseLocalPosition;

            return;
        }

        float time =
            Time.time * _shakeSpeed;

        float offsetX =
            (Mathf.PerlinNoise(time, 0f) - 0.5f) *
            2f;

        float offsetY =
            (Mathf.PerlinNoise(0f, time) - 0.5f) *
            2f;

        Vector3 shakeOffset =
            new Vector3(
                offsetX,
                offsetY,
                0f
            ) *
            _maxShakeAmount *
            intensity;

        _playerCamera.transform.localPosition =
            _baseLocalPosition + shakeOffset;
    }

    private void ResetCameraPosition()
    {
        if (_playerCamera == null)
        {
            return;
        }

        _playerCamera.transform.localPosition =
            _baseLocalPosition;
    }
}