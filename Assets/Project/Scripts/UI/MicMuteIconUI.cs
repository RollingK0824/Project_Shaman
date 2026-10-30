using UnityEngine;

// HUD 마이크 음소거 아이콘. 음소거일 때만 _icon을 켬
public class MicMuteIconUI : MonoBehaviour
{
    [SerializeField]
    private GameObject _icon;

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
        if (_icon == null)
        {
            return;
        }

        _icon.SetActive(muted);
    }
}
