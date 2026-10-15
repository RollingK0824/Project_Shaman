using UnityEngine;
using System;


// 판정자(서버 or 오프라인)가 아이템 사용을 실행할 때 받는 입력
// 카메라, Interactor, UI를 참조하지 않기 위해 조준값을 직접 가지고 다님

public readonly struct ItemUseContext
{
    public readonly GameObject Owner;
    public readonly ItemData Data;
    public readonly Vector3 AimOrigin;          // 로컬 클라 카메라 위치
    public readonly Vector3 AimDirection;       // 정규화된 방향
    public readonly Action<int> Consume;        // 소모 요청


    public ItemUseContext(GameObject owner, ItemData data, Vector3 aimOrigin, Vector3 aimDirection, Action<int> consume = null)
    {
        Owner = owner;
        Data = data;
        AimOrigin = aimOrigin;
        AimDirection = aimDirection.normalized;
        Consume = consume;
    }

    // 판정자가 소모 콜백만 채워서 다시 만들 때 사용
    public ItemUseContext WithConsume(Action<int> consume) => new ItemUseContext(Owner, Data, AimOrigin, AimDirection, consume);
}
