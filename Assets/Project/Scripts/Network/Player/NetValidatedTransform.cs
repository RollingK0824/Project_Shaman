using UnityEngine;
using Mirror;

[AddComponentMenu("Network/Net Validated Transform")]
public class NetValidatedTransform : NetworkTransformUnreliable
{
    protected override void Configure()
    {
        base.Configure();

        // 서버가 승인한 위치를 클라이언트에 전달
        syncDirection = SyncDirection.ServerToClient;
    }
}
