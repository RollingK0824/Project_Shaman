using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData _itemData;

    private bool _isCollected;

    public ItemData ItemData => _itemData;

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
        if (_isCollected ||
            _itemData == null)
        {
            return;
        }

        PlayerInventory inventory =
            interactor.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            return;
        }

        bool success =
            inventory.TryAddItem(_itemData);

        if (!success)
        {
            return;
        }

        _isCollected = true;

        Debug.Log(
            $"[WorldItem] 획득 완료: " +
            $"{_itemData.DisplayName}",
            gameObject
        );

        gameObject.SetActive(false);
    }
}