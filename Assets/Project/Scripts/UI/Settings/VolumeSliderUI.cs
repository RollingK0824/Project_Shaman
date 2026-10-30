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
        _slider.SetValueWithoutNotify(v);
        UpdateLabel(v);
    }

    private void HandleChanged(float v)
    {
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
