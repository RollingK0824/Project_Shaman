using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : SceneSingleton<UIManager>
{
    private readonly List<UIPanel> _openPanels = new List<UIPanel>();
    private NetworkIdentity _trackedPlayer;
    private ShamanInput _actions;

    public bool IsMenuOpen => _openPanels.Count > 0;
    public bool IsOpen(UIPanel panel) => _openPanels.Contains(panel);

    public event Action<bool> MenuOpenChanged;


    public void Open(UIPanel panel)
    {
        if (panel == null || _openPanels.Contains(panel))
        {
            GameLog.Log("Unable to open UI.");
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
            GameLog.Log("Unable to close UI.");
            return;
        }

        panel.gameObject.SetActive(false);
        RefreshInputState();
    }

    private void OnEnable()
    {
        _actions = InputManager.Instance.Actions;
        _actions.UI.Cancel.performed += OnCancel;
    }

    private void OnDisable()
    {
        // 종료 중 InputManager가 새로 생기지 않도록 캐시된 _actions 사용
        if (_actions != null) _actions.UI.Cancel.performed -= OnCancel;
    }

    // Esc(UI.Cancel)는 맨 위 패널만 닫는다
    private void OnCancel(InputAction.CallbackContext context)
    {
        if (IsMenuOpen)
        {
            Close(_openPanels[_openPanels.Count - 1]);
        }
    }

    private void Update()
    {
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
