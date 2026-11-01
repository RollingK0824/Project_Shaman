using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderUI : MonoBehaviour
{
    [SerializeField]
    private SoundCategory _category;

    // 음성 채팅 볼륨 (SoundCategory에 Voice가 없어서 VoiceSettings 사용, _category 무시)
    [SerializeField]
    private bool _voice;

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

        // Slider works in whole percent so arrow keys step by 1% (non-whole sliders step by 10% of the range)
        _slider.minValue = 0f;
        _slider.maxValue = 100f;
        _slider.wholeNumbers = true;
        _slider.onValueChanged.AddListener(HandleChanged);
    }

    private void UpdateLabel(float v)
    {
        if (_valueText == null)
        {
            return;
        }

        _valueText.SetText($"{Mathf.RoundToInt(v * 100)}%");
    }

    private void OnEnable()
    {
        if (_slider == null)
        {
            return;
        }

        float v = _voice ? VoiceSettings.Volume : SoundManager.Instance.GetVolume(_category);
        _slider.SetValueWithoutNotify(v * 100f);
        UpdateLabel(v);
    }

    private void HandleChanged(float percent)
    {
        float v = percent / 100f;
        if (_voice)
        {
            VoiceSettings.SetVolume(v);
        }
        else
        {
            SoundManager.Instance.SetVolume(_category, v);
        }
        UpdateLabel(v);
    }
}
