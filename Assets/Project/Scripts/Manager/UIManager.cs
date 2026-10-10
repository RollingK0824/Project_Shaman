using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : SceneSingleton<UIManager>
{
    private readonly List<UIPanel> _openPanels = new List<UIPanel>();
    private NetworkIdentity _trackedPlayer;

    public bool IsMenuOpen => _openPanels.Count > 0;

    public event Action<bool> MenuOpenChanged;


    public void Open(UIPanel panel)
    {
        if (panel == null || _openPanels.Contains(panel))
        {
            Debug.Log("Unable to open UI.");
            return;
        }

        _openPanels.Add(panel);
        panel.gameObject.SetActive(true);
        RefreshInputState();
    }

    public void Close(UIPanel panel)
    {
        if (panel == null || !_openPanels.Remove(panel))
        {
            Debug.Log("Unable to close UI.");
            return;
        }

        panel.gameObject.SetActive(false);
        RefreshInputState();
    }

    private void Update()
    {
        if (IsMenuOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close(_openPanels[_openPanels.Count - 1]);
        }

        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer != _trackedPlayer)
        {
            _trackedPlayer = localPlayer;
            RefreshInputState();
        }
    }

    private void RefreshInputState()
    {
        bool needsCursor = false;
        foreach (UIPanel panel in _openPanels)
        {
            if (panel.UnlocksCursor)
            {
                needsCursor = true;
                break;
            }
        }

        bool lockCursor = _trackedPlayer != null && !needsCursor;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        if (_trackedPlayer != null && _trackedPlayer.TryGetComponent(out PlayerInputReader inputReader))
        {
            inputReader.SetGameplayInputBlocked(IsMenuOpen);
        }

        MenuOpenChanged?.Invoke(IsMenuOpen);
    }

    protected override void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        base.OnDestroy();
    }
}
