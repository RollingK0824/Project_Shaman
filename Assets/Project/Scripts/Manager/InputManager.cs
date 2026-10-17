using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : GlobalSingleton<InputManager>
{
    private const string BINDINGS_KEY = "Keybinds";

    private ShamanInput _actions;
    public event Action BindingsChanged;

    public ShamanInput Actions
    {
        get
        {
            if (_actions == null)
            {
                Create();
            }
            return _actions;
        }
    }

    private void Create()
    {
        _actions = new ShamanInput();

        string json = PlayerPrefs.GetString(BINDINGS_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _actions.asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                // 저장된 값이 깨졌으면 기본 키로 시작
                Debug.LogWarning($"[InputManager] Saved keybinds could not be loaded, using defaults. {e.Message}");
                PlayerPrefs.DeleteKey(BINDINGS_KEY);
            }
        }

        _actions.Enable();
    }

    // 기본값과 다른 키만 저장
    public void SaveBindings()
    {
        if (_actions == null)
        {
            return;
        }

        PlayerPrefs.SetString(BINDINGS_KEY, _actions.asset.SaveBindingOverridesAsJson());
        BindingsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (_actions == null)
        {
            return;
        }

        _actions.Disable();
        _actions.Dispose();
        _actions = null;
    }
}
