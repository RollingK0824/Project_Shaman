using UnityEngine;
using Mirror;

public class NetGM : NetworkBehaviour
{
    public override void OnStartServer()
    {
        GameManager.Instance.OnWin += () => RpcAnnounceResult(true);
        GameManager.Instance.OnWin -= () => RpcAnnounceResult(false);
    }

    [Server] public void ServerReportNpcDied() => GameManager.Instance.UpdateNpcCount();
    [Server] public void ServerReportGhostExorcised() => GameManager.Instance.UpdateGhostCount();

    [ClientRpc]
    private void RpcAnnounceResult(bool win)
    {
        if (isServer)
        {
            return;
        }

        if (win)
        {
            GameManager.Instance.OnWin?.Invoke();
        }
        else
        {
            GameManager.Instance.OnLose?.Invoke();
        }
    }
}
