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
        _rebind?.Dispose();
        _rebind = null;

        if (_actions == null)
        {
            return;
        }

        _actions.Disable();
        _actions.Dispose();
        _actions = null;
    }


    private InputActionRebindingExtensions.RebindingOperation _rebind;
    public bool IsRebinding { get; private set; }

    // true if the last rebind was undone because the key was already used (false for Esc / busy)
    public bool LastRebindRejected { get; private set; }

    public void StartRebind(InputAction action, int bindingIndex, Action<bool> onDone)
    {
        if (IsRebinding)
        {
            LastRebindRejected = false;
            onDone?.Invoke(false);
            return;
        }

        string oldOverride = action.bindings[bindingIndex].overridePath;
        IsRebinding = true;
        Actions.UI.Disable();
        action.Disable();

        _rebind = action.PerformInteractiveRebinding(bindingIndex)
        .WithControlsExcluding("<Mouse>/position")
        .WithControlsExcluding("<Mouse>/delta")
        .WithCancelingThrough("<Keyboard>/escape")
        .OnMatchWaitForAnother(0.1f)
        .OnComplete(_ => Finish(action, bindingIndex, oldOverride, true, onDone))
        .OnCancel(_ => Finish(action, bindingIndex, oldOverride, false, onDone))
        .Start();
    }

    private void Finish(InputAction action, int bindingIndex, string oldOverride, bool changed, Action<bool> onDone)
    {
        _rebind.Dispose();
        _rebind = null;
        action.Enable();
        Actions.UI.Enable();
        IsRebinding = false;
        LastRebindRejected = false;

        // 4. Check
        if (changed && HasConflict(action, bindingIndex))
        {
            LastRebindRejected = true;
            // put the old key back
            if (string.IsNullOrEmpty(oldOverride)) action.RemoveBindingOverride(bindingIndex);
            else action.ApplyBindingOverride(bindingIndex, oldOverride);
            changed = false;
        }

        if (changed) SaveBindings();
        onDone?.Invoke(changed);
    }

    private bool HasConflict(InputAction action, int bindingIndex)
    {
        string newPath = action.bindings[bindingIndex].effectivePath;
        InputActionMap own = action.actionMap;
        InputActionMap other = own == Actions.UI.Get() ? null : Actions.UI.Get();

        return UsesPath(own, newPath, action, bindingIndex)
            || (other != null && UsesPath(other, newPath, action, bindingIndex))
            || (own == Actions.UI.Get() && (UsesPath(Actions.Player.Get(), newPath, action, bindingIndex)
            || UsesPath(Actions.Spectator.Get(), newPath, action, bindingIndex)));
    }

    private static bool UsesPath(InputActionMap map, string path, InputAction self, int selfIndex)
    {
        foreach (InputAction a in map.actions)
        {
            for (int i = 0; i < a.bindings.Count; i++)
            {
                if (a == self && i == selfIndex) continue;   // itself
                if (a.bindings[i].isComposite) continue;      // "2D Vector" parent, not a key
                if (a.bindings[i].effectivePath == path) return true;
            }
        }
        return false;
    }

    public static int FindPart(InputAction action, string partName)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isPartOfComposite && action.bindings[i].name == partName) return i;
        }
        return -1;
    }

    public void ResetBinding(InputAction action, int bindingIndex)
    {
        action.RemoveBindingOverride(bindingIndex);
        SaveBindings();
    }

    public void ResetAll()
    {
        Actions.asset.RemoveAllBindingOverrides();
        SaveBindings();
    }

    public static string GetDisplay(InputAction action, int bindingIndex)
    {
        return action.GetBindingDisplayString(bindingIndex);
    }
}
