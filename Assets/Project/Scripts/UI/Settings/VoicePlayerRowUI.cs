using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어별 음성 한 줄: 이름 + 볼륨 슬라이더 + 음소거 토글 + 말하는 중 아이콘. 이 PC에서만 적용
public class VoicePlayerRowUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private Slider _volumeSlider;

    [SerializeField]
    private TMP_Text _volumeText;

    [SerializeField]
    private Toggle _muteToggle;

    [SerializeField]
    private GameObject _speakingIcon;

    private NetPlayerVoice _player;

    private void Awake()
    {
        if (_volumeSlider != null)
        {
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 1f;
            _volumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        if (_muteToggle != null)
        {
            _muteToggle.onValueChanged.AddListener(HandleToggleChanged);
        }
    }

    public void Bind(NetPlayerVoice player)
    {
        Unbind();

        _player = player;
        _player.MutedLocallyChanged += HandleMutedChanged;
        _player.LocalVolumeChanged += HandleVolumeChanged;
        HandleMutedChanged(_player.IsMutedLocally);
        HandleVolumeChanged(_player.LocalVolume);
    }

    // 이름 SyncVar가 줄을 만든 뒤에 올 수 있어서 매 프레임 갱신
    private void Update()
    {
        if (_player == null)
        {
            return;
        }

        if (_nameText != null)
        {
            _nameText.SetText(_player.DisplayName);
        }

        if (_speakingIcon != null)
        {
            _speakingIcon.SetActive(_player.IsSpeaking);
        }
    }

    private void HandleToggleChanged(bool muted)
    {
        if (_player == null)
        {
            return;
        }

        _player.SetMutedLocally(muted);
    }

    private void HandleSliderChanged(float v)
    {
        if (_player == null)
        {
            return;
        }

        _player.SetLocalVolume(v);
        UpdateVolumeLabel(v);
    }

    private void HandleVolumeChanged(float v)
    {
        if (_volumeSlider != null)
        {
            _volumeSlider.SetValueWithoutNotify(v);
        }
        UpdateVolumeLabel(v);
    }

    private void UpdateVolumeLabel(float v)
    {
        if (_volumeText == null)
        {
            return;
        }

        _volumeText.SetText($"{Mathf.RoundToInt(v * 100)}%");
    }

    private void HandleMutedChanged(bool muted)
    {
        if (_muteToggle == null)
        {
            return;
        }

        _muteToggle.SetIsOnWithoutNotify(muted);
    }

    private void Unbind()
    {
        // 플레이어 오브젝트가 먼저 없어졌을 수 있음
        if (_player != null)
        {
            _player.MutedLocallyChanged -= HandleMutedChanged;
            _player.LocalVolumeChanged -= HandleVolumeChanged;
        }
        _player = null;
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
