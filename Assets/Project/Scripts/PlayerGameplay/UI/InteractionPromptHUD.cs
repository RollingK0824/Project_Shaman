using UnityEngine;
using TMPro;

public class InteractionPromptHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInteractor _playerInteractor;

    [Header("Display")]
    [SerializeField] private bool _showUnavailableStatus = true;
    [SerializeField] private int _fontSize = 24;
    [SerializeField] [Range(0.3f, 0.9f)] private float _verticalPosition = 0.62f;
    [SerializeField] private float _height = 40f;

    [Header("Panel (비우면 OnGUI로 표시)")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _panelText;
    [SerializeField] private GameObject _panelKeyIcon;

    private GUIStyle _style;

    private void Awake()
    {
        if (_playerInteractor == null)
        {
            _playerInteractor = GetComponentInParent<PlayerInteractor>();
        }
    }

    private void Update()
    {
        if (_panel == null) return;

        bool show = TryGetPrompt(out string prompt, out bool canInteract);
        _panel.SetActive(show);
        if (!show) return;

        _panelText.text = prompt;
        if (_panelKeyIcon != null) _panelKeyIcon.SetActive(canInteract);
    }

    private void OnDisable()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // 표시할 안내가 있으면 true. 패널과 OnGUI가 같은 규칙을 쓰도록 한 곳에서 판정
    private bool TryGetPrompt(out string prompt, out bool canInteract)
    {
        prompt = null;
        canInteract = false;

        if (_playerInteractor == null || !_playerInteractor.CanInteract) return false;

        IInteractable target = _playerInteractor.CurrentTarget;
        if (target == null) return false;

        prompt = target.InteractionPrompt;
        if (string.IsNullOrWhiteSpace(prompt)) return false;

        canInteract = target.CanInteract(_playerInteractor.gameObject);
        return canInteract || _showUnavailableStatus;
    }

    private void OnGUI()
    {
        if (_panel != null) return;
        if (!TryGetPrompt(out string prompt, out bool canInteract)) return;

        EnsureStyle();

        string displayText = canInteract ? $"[F] {prompt}" : prompt;
        Rect rect = new Rect(
            0f,
            Screen.height * _verticalPosition,
            Screen.width,
            _height
        );

        GUI.Label(rect, displayText, _style);
    }

    private void EnsureStyle()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
        }

        _style.alignment = TextAnchor.MiddleCenter;
        _style.fontSize = _fontSize;
        _style.fontStyle = FontStyle.Bold;
        _style.normal.textColor = Color.white;
    }
}
