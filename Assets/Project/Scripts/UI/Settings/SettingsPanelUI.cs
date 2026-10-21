using UnityEngine;

public class SettingsPanelUI : UIPanel
{
    [SerializeField]
    private KeybindUIPanel _keybindPanel;

    [SerializeField]
    bool _closeOnEsc = true;

    public static SettingsPanelUI Current { get; private set; }

    private void OnEnable()
    {
        Current = this;

        if (_keybindPanel == null)
        {
            return;
        }

        _keybindPanel.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (_closeOnEsc && InputManager.Instance.Actions.UI.Cancel.WasPerformedThisFrame())
        {
            Back();
        }
    }

    // Esc / ✕: closes the keybind panel first, then the settings panel
    public void Back()
    {
        if (_keybindPanel != null && _keybindPanel.gameObject.activeSelf)
        {
            _keybindPanel.Close();
            return;
        }

        Close();
    }
}
