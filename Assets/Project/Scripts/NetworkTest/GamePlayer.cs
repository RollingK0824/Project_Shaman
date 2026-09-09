using UnityEngine;
using Mirror;

public class GamePlayer : NetworkBehaviour
{
    [SyncVar] public string playerName;
    public float moveSpeed = 300f;

    void Update()
    {
        if (!isLocalPlayer) return; // 본인만 입력 처리

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        transform.position += new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;

        // 임시 디버그 키
        if (Input.GetKeyDown(KeyCode.Escape))
            CmdEndGame();
    }

    [Command]
    void CmdEndGame()
    {
        // 전원 메인 메뉴로 돌아감
        NetworkManager.singleton.StopHost();
    }
}
