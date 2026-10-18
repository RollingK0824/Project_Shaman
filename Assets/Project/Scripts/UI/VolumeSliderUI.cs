using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderUI : MonoBehaviour
{
    [SerializeField]
    private SoundCategory _category;

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

        _slider.SetValueWithoutNotify(SoundManager.Instance.GetVolume(_category));
        UpdateLabel(SoundManager.Instance.GetVolume(_category));
    }

    private void HandleChanged(float v)
    {
        SoundManager.Instance.SetVolume(_category, v);
        UpdateLabel(v);
    }
}
