using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplaySettingsUI : MonoBehaviour
{
    [SerializeField]
    private Slider _sensitivitySlider;
    [SerializeField]
    private TMP_Text _sensitivityText;

    [SerializeField]
    private Slider _fovSlider;
    [SerializeField]
    private TMP_Text _fovText;

    [SerializeField]
    private Toggle _invertYToggle;


    private void Awake()
    {
        // Ranges come from GameSettings, so the Inspector values don't matter
        if (_sensitivitySlider != null)
        {
            _sensitivitySlider.minValue = GameSettings.MIN_SENSITIVITY;
            _sensitivitySlider.maxValue = GameSettings.MAX_SENSITIVITY;
            _sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
        }

        if (_fovSlider != null)
        {
            _fovSlider.minValue = GameSettings.MIN_FOV;
            _fovSlider.maxValue = GameSettings.MAX_FOV;
            _fovSlider.wholeNumbers = true;
            _fovSlider.onValueChanged.AddListener(HandleFovChanged);
        }

        if (_invertYToggle != null)
        {
            _invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);
        }
    }

    // Runs every time the page is shown, so it always matches the saved values
    private void OnEnable()
    {
        if (_sensitivitySlider != null)
        {
            _sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        }
        UpdateSensitivityLabel(GameSettings.MouseSensitivity);

        if (_fovSlider != null)
        {
            _fovSlider.SetValueWithoutNotify(GameSettings.FieldOfView);
        }
        UpdateFovLabel(GameSettings.FieldOfView);

        if (_invertYToggle != null)
        {
            _invertYToggle.SetIsOnWithoutNotify(GameSettings.InvertY);
        }
    }

    private void HandleSensitivityChanged(float v)
    {
        GameSettings.SetMouseSensitivity(v);
        UpdateSensitivityLabel(v);
    }

    private void UpdateSensitivityLabel(float v)
    {
        if (_sensitivityText != null) _sensitivityText.SetText($"{v:0.0}x");
    }


    private void HandleFovChanged(float v)
    {
        GameSettings.SetFieldOfView(v);
        UpdateFovLabel(v);
    }

    private void UpdateFovLabel(float v)
    {
        if (_fovText != null) _fovText.SetText($"{v:0}");
    }


    private void HandleInvertYChanged(bool on)
    {
        GameSettings.SetInvertY(on);
    }
}
