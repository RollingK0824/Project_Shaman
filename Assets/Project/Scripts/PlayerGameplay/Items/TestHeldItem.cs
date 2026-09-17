using UnityEngine;

public class TestHeldItem : ItemBase
{
    public override void OnUseStarted()
    {
        Debug.Log(
            $"[Item] 사용 시작: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseCanceled()
    {
        Debug.Log(
            $"[Item] 사용 종료: {Data.DisplayName}",
            gameObject
        );
    }
}