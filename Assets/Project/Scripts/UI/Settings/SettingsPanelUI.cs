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

    private void Update()
    {
        if (_closeOnEsc && InputManager.Instance.Actions.UI.Cancel.WasPerformedThisFrame())
        {
            Back();
        }
    }

    public void Back()
    {
        if (_keybindPanel == null)
        {
            return;
        }

        _keybindPanel.Close();
    }
}
