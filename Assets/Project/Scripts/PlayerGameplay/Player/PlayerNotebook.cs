using System;
using UnityEngine;

// Permanent default possession. The dedicated N action opens its UI without occupying a quickslot.
[DisallowMultipleComponent]
public class PlayerNotebook : MonoBehaviour
{
    [SerializeField] private ItemData _itemData;
    public ItemData ItemData => _itemData;
    public bool HasNotebook => true;
    private void Start()
    {
        if (_itemData == null || !PlayerLocalPresentation.IsLocal(gameObject)) return;
        var inventory = GetComponent<PlayerInventory>();
        // An existing inventory/snapshot keeps the player's chosen slot arrangement.
        if (inventory == null || inventory.Contains(_itemData) || !inventory.TryAddItem(_itemData)) return;
    }
    public event Action NotebookOpenRequested;
    public bool RequestOpen()
    {
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject) || !PlayerLocalPresentation.IsLocal(gameObject)) return false;
        NotebookOpenRequested?.Invoke();
        return true;
    }
}
