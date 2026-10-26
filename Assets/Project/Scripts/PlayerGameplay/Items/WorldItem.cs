using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData _itemData;

    private bool _isCollected;

    public ItemData ItemData => _itemData;

    private void OnEnable()
    {
        // 풀에서 재사용할 때 이전 획득 상태를 남기지 않습니다.
        _isCollected = false;
        if (_itemData != null && _itemData.ItemId == "item_lantern" && GetComponent<LanternWorldPresentation>() == null)
            gameObject.AddComponent<LanternWorldPresentation>();
    }

    public string InteractionPrompt
    {
        get
        {
            if (_itemData == null)
            {
                return "아이템 줍기";
            }

            return $"{_itemData.DisplayName} 줍기";
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        // 비활성 컴포넌트도 인터페이스 검색으로 발견될 수 있습니다.
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(interactor)) return false;

        if (_isCollected)
        {
            return false;
        }

        if (_itemData == null)
        {
            return false;
        }


        PlayerInventory inventory =
            interactor.GetComponent<PlayerInventory>();

        return inventory != null;
    }


    public void Interact(GameObject interactor)
    {

        if (!CanInteract(interactor))
        {
            return;
        }

        // 판정자가 인벤토리 추가와 월드 아이템 제거를 확정하도록 요청만 보낸다.
        PlayerEvents.RaisePickupRequested(interactor, this);
    }

    // 오프라인 전용 획득 확정 시 아이템 숨김
    // 네트워크에서 서버가 오브젝트를 제거하기에 사용하지는 않음
    public void ApplyCollected()
    {
        _isCollected = true;
        gameObject.SetActive(false);
    }
}
