using Mirror;
using UnityEngine;

/// <summary>Enables local input and presentation only for this connection's player.</summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetPlayer : NetworkBehaviour
{
    [SerializeField] private Behaviour[] _localOnlyComponents;

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetLocalComponents(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetLocalComponents(isLocalPlayer);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetLocalComponents(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStopLocalPlayer()
    {
        SetLocalComponents(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        base.OnStopLocalPlayer();
    }

    public override void OnStopClient()
    {
        SetLocalComponents(false);
        base.OnStopClient();
    }

    private void SetLocalComponents(bool active)
    {
        if (_localOnlyComponents == null) return;
        foreach (Behaviour component in _localOnlyComponents)
        {
            if (component != null && component != this)
                component.enabled = active;
        }
    }
}
