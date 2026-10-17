using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SensitivitySliderUI : MonoBehaviour
{
    [SerializeField]
    private Slider _slider;

    [SerializeField]
    private TMP_Text _valueText;

    private void Awake()
    {
        if (_slider == null)
        {
            return;
        }

        _slider.onValueChanged.AddListener(HandleChanged);
    }

    private void UpdateLabel(float v)
    {
        if (_valueText == null)
        {
            return;
        }

        _valueText.SetText($"{v:0.0}x");
    }

    private void OnEnable()
    {
        if (_slider == null)
        {
            return;
        }

        _slider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        UpdateLabel(GameSettings.MouseSensitivity);
    }

    private void HandleChanged(float v)
    {
        GameSettings.SetMouseSensitivity(v);
        UpdateLabel(v);
    }
}
