using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    [SerializeField]
    private Toggle _ssaoToggle;

    [SerializeField]
    private TMP_Dropdown _antiAliasingDropdown;

    [SerializeField]
    private TMP_Dropdown _shadowDropdown;

    [SerializeField]
    private Slider _brightnessSlider;
    [SerializeField]
    private TMP_Text _brightnessText;

    [SerializeField]
    private Slider _contrastSlider;
    [SerializeField]
    private TMP_Text _contrastText;


    private void Awake()
    {
        if (_ssaoToggle != null)
        {
            _ssaoToggle.onValueChanged.AddListener(HandleSsaoChanged);
        }

        if (_antiAliasingDropdown != null)
        {
            _antiAliasingDropdown.ClearOptions();
            _antiAliasingDropdown.AddOptions(new List<string>(GraphicOptions.AntiAliasingLabels));
            _antiAliasingDropdown.onValueChanged.AddListener(HandleAntiAliasingChanged);
        }

        if (_shadowDropdown != null)
        {
            _shadowDropdown.ClearOptions();
            _shadowDropdown.AddOptions(new List<string>(GraphicOptions.ShadowLabels));
            _shadowDropdown.onValueChanged.AddListener(HandleShadowChanged);
        }

        if (_brightnessSlider != null)
        {
            _brightnessSlider.minValue = GraphicOptions.MIN_BRIGHTNESS;
            _brightnessSlider.maxValue = GraphicOptions.MAX_BRIGHTNESS;
            _brightnessSlider.wholeNumbers = false;
            _brightnessSlider.onValueChanged.AddListener(HandleBrightnessChanged);
        }

        if (_contrastSlider != null)
        {
            _contrastSlider.minValue = GraphicOptions.MIN_CONTRAST;
            _contrastSlider.maxValue = GraphicOptions.MAX_CONTRAST;
            _contrastSlider.wholeNumbers = true;
            _contrastSlider.onValueChanged.AddListener(HandleContrastChanged);
        }
    }

    private void OnEnable()
    {
        if (_ssaoToggle != null)
        {
            _ssaoToggle.SetIsOnWithoutNotify(GraphicOptions.Ssao);
        }

        if (_antiAliasingDropdown != null)
        {
            _antiAliasingDropdown.SetValueWithoutNotify((int)GraphicOptions.AntiAliasing);
        }

        if (_shadowDropdown != null)
        {
            _shadowDropdown.SetValueWithoutNotify((int)GraphicOptions.ShadowQuality);
        }

        if (_brightnessSlider != null)
        {
            _brightnessSlider.SetValueWithoutNotify(GraphicOptions.Brightness);
        }
        UpdateBrightnessLabel(GraphicOptions.Brightness);

        if (_contrastSlider != null)
        {
            _contrastSlider.SetValueWithoutNotify(GraphicOptions.Contrast);
        }
        UpdateContrastLabel(GraphicOptions.Contrast);
    }

    private void HandleSsaoChanged(bool on)
    {
        GraphicOptions.SetSsao(on);
    }

    private void HandleAntiAliasingChanged(int index)
    {
        GraphicOptions.SetAntiAliasing((AntiAliasingOption)index);
    }

    private void HandleShadowChanged(int index)
    {
        GraphicOptions.SetShadowQuality((ShadowQualityOption)index);
    }


    private void HandleBrightnessChanged(float v)
    {
        GraphicOptions.SetBrightness(v);
        UpdateBrightnessLabel(v);
    }

    private void UpdateBrightnessLabel(float v)
    {
        if (_brightnessText != null) _brightnessText.SetText($"{Mathf.RoundToInt(v * 100)}%");
    }


    private void HandleContrastChanged(float v)
    {
        GraphicOptions.SetContrast(v);
        UpdateContrastLabel(v);
    }

    private void UpdateContrastLabel(float v)
    {
        if (_contrastText != null) _contrastText.SetText($"{v:0}");
    }
}
