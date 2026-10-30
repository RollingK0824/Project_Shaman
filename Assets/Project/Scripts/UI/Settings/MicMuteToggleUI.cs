using UnityEngine;
using UnityEngine.UI;

// 설정의 "마이크 음소거" 토글. 단축키로 바뀌어도 따라감
public class MicMuteToggleUI : MonoBehaviour
{
    [SerializeField]
    private Toggle _toggle;

    private void Awake()
    {
        if (_toggle == null)
        {
            return;
        }

        _toggle.onValueChanged.AddListener(VoiceSettings.SetMicMuted);
    }

    private void OnEnable()
    {
        HandleMicMutedChanged(VoiceSettings.MicMuted);
        VoiceSettings.MicMutedChanged += HandleMicMutedChanged;
    }

    private void OnDisable()
    {
        VoiceSettings.MicMutedChanged -= HandleMicMutedChanged;
    }

    private void HandleMicMutedChanged(bool muted)
    {
        if (_toggle == null)
        {
            return;
        }

        _toggle.SetIsOnWithoutNotify(muted);
    }
}
