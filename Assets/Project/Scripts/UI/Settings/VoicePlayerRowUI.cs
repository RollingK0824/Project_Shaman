using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어별 음소거 한 줄: 이름 + 음소거 토글 + 말하는 중 아이콘. 이 PC에서만 적용
public class VoicePlayerRowUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private Toggle _muteToggle;

    [SerializeField]
    private GameObject _speakingIcon;

    private NetPlayerVoice _player;

    private void Awake()
    {
        if (_muteToggle == null)
        {
            return;
        }

        _muteToggle.onValueChanged.AddListener(HandleToggleChanged);
    }

    public void Bind(NetPlayerVoice player)
    {
        Unbind();

        _player = player;
        _player.MutedLocallyChanged += HandleMutedChanged;
        HandleMutedChanged(_player.IsMutedLocally);
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
        }
        _player = null;
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
