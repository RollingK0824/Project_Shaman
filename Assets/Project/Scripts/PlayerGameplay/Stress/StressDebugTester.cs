using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerStressController))]
public class StressDebugTester : MonoBehaviour
{
    [SerializeField] private float _changeAmount = 10f;

    private PlayerStressController _stressController;

    private void Awake()
    {
        _stressController =
            GetComponent<PlayerStressController>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            _stressController.AddStress(
                _changeAmount
            );

            Debug.Log(
                $"[Stress] 증가 → " +
                $"{_stressController.CurrentStress:0}"
            );
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            _stressController.ReduceStress(
                _changeAmount
            );

            Debug.Log(
                $"[Stress] 감소 → " +
                $"{_stressController.CurrentStress:0}"
            );
        }
    }
}