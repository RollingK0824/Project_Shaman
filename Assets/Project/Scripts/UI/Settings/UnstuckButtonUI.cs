using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UnstuckButtonUI : MonoBehaviour
{

    [SerializeField]
    private float _cooldown = 60f;

    [SerializeField]
    private Button _button;

    [SerializeField]
    private TMP_Text _label;

    [SerializeField]
    private UnityEvent _onUnstuck;

    private float _readyTime;

    // Seconds the label shows (0 = ready), so it rebuilds once a second instead of every frame
    private int _shownSeconds = -1;

    public float Remaining => Mathf.Max(0f, _readyTime - Time.unscaledTime);
    public bool IsReady => Remaining <= 0f;

    private void Awake()
    {
        if (_button == null)
        {
            return;
        }

        _button.onClick.AddListener(HandleClicked);
    }

    private void HandleClicked()
    {
        if (!IsReady)
        {
            return;
        }

        _onUnstuck?.Invoke();
        StartCooldown();
    }

    public void StartCooldown()
    {
        _readyTime = Time.unscaledTime + _cooldown;
    }

    private void Update()
    {
        if (_button == null || _label == null)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(Remaining);
        if (seconds == _shownSeconds)
        {
            return;
        }

        _shownSeconds = seconds;
        _button.interactable = seconds == 0;
        _label.SetText(seconds == 0 ? "끼임 탈출" : $"({seconds}초)");
    }
}
