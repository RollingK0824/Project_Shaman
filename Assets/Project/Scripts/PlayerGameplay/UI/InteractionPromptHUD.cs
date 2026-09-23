using UnityEngine;

public class InteractionPromptHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInteractor _playerInteractor;

    [Header("Display")]
    [SerializeField] private bool _showUnavailableStatus = true;
    [SerializeField] private int _fontSize = 24;
    [SerializeField] [Range(0.3f, 0.9f)] private float _verticalPosition = 0.62f;
    [SerializeField] private float _height = 40f;

    private GUIStyle _style;

    private void Awake()
    {
        if (_playerInteractor == null)
        {
            _playerInteractor = GetComponentInParent<PlayerInteractor>();
        }
    }

    private void OnGUI()
    {
        if (_playerInteractor == null || !_playerInteractor.CanInteract)
        {
            return;
        }

        IInteractable target = _playerInteractor.CurrentTarget;
        if (target == null)
        {
            return;
        }

        string prompt = target.InteractionPrompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        bool canInteract = target.CanInteract(_playerInteractor.gameObject);
        if (!canInteract && !_showUnavailableStatus)
        {
            return;
        }

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
