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
        _button.interactable = IsReady;
        _label.SetText(IsReady ? "끼임 탈출" : $"({Mathf.CeilToInt(Remaining)}초)");
    }
}
