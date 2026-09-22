using UnityEngine;

public class TestHeldItem : ItemBase
{
    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[TestItem] 장착됨: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUnequipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[TestItem] 장착 해제: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseStarted()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[TestItem] 사용 시작: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseCanceled()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[TestItem] 사용 종료: {Data.DisplayName}",
            gameObject
        );
    }
}