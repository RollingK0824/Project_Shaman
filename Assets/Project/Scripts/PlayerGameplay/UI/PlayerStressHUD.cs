using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStressHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerStressController _stress;
    [SerializeField] private PlayerHealth _health;

    [Header("Display")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private bool _hideOnDeath = true;

    [Header("Stage Colors")]
    [SerializeField] private Color _lowColor = new Color(0.57f, 0.77f, 0.68f);
    [SerializeField] private Color _midColor = new Color(0.94f, 0.72f, 0.36f);
    [SerializeField] private Color _highColor = new Color(0.96f, 0.39f, 0.34f);

    private NetworkIdentity _identity;

    private void Awake()
    {
        if (_stress == null) _stress = GetComponentInParent<PlayerStressController>();
        if (_health == null) _health = GetComponentInParent<PlayerHealth>();
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        _identity = GetComponentInParent<NetworkIdentity>();
    }

    private void OnEnable()
    {
        if (_stress != null)
        {
            _stress.StressChanged += HandleStressChanged;
            _stress.StressLevelChanged += HandleLevelChanged;
        }
        if (_health != null) _health.Died += HandleDied;
        Refresh();
    }

    private void OnDisable()
    {
        if (_stress != null)
        {
            _stress.StressChanged -= HandleStressChanged;
            _stress.StressLevelChanged -= HandleLevelChanged;
        }
        if (_health != null) _health.Died -= HandleDied;
        if (_canvas != null) _canvas.enabled = false;
    }

    private void HandleStressChanged(float value) => Refresh();
    private void HandleLevelChanged(PlayerStressController.StressLevel level) => Refresh();
    private void HandleDied(GameObject source) => Refresh();

    private void Refresh()
    {
        bool localView = (!NetworkClient.active && !NetworkServer.active) ||
            (_identity != null && _identity.isLocalPlayer);
        if (_canvas != null)
            _canvas.enabled = _stress != null && localView &&
                (!_hideOnDeath || _health == null || !_health.IsDead);
        if (_stress == null) return;

        Color color = _lowColor;
        string label = "안정";
        if (_stress.CurrentLevel == PlayerStressController.StressLevel.Mid)
        {
            color = _midColor;
            label = "긴장";
        }
        else if (_stress.CurrentLevel == PlayerStressController.StressLevel.High)
        {
            color = _highColor;
            label = "위험";
        }

        if (_valueText != null) _valueText.SetText("{0:1} / 100", _stress.CurrentStress);
        if (_levelText != null)
        {
            _levelText.text = label;
            _levelText.color = color;
        }
        if (_fill != null)
        {
            _fill.color = color;
            _fill.rectTransform.anchorMax = new Vector2(_stress.NormalizedStress, 1f);
        }
    }
}
