using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData _itemData;

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
        return
            _itemData != null &&
            interactor.GetComponent<PlayerInventory>() != null;
    }

    public void Interact(GameObject interactor)
    {
        PlayerInventory inventory =
            interactor.GetComponent<PlayerInventory>();

        if (inventory == null ||
            _itemData == null)
        {
            return;
        }

        bool success =
            inventory.TryAddItem(_itemData);

        if (!success)
        {
            return;
        }

        Debug.Log(
            $"[WorldItem] 획득 완료: " +
            $"{_itemData.DisplayName}",
            gameObject
        );

        // 프로토타입에서는 비활성화.
        // 이후 PoolManager가 연결되면
        // Pool 반환 방식으로 교체 가능.
        gameObject.SetActive(false);
    }
}