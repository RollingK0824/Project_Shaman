using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerCraftingController : MonoBehaviour
{
    [SerializeField] private PlayerInventory _inventory;
    public PlayerInventory Inventory => _inventory;
    public event Action<CraftingRecipeData> Crafted;
    public event Action<CraftingRecipeData> CraftRequested;
    private Func<bool> _authorityCheck;

    private void Awake()
    {
        if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
    }

    // Network integration can grant server execution after validating the request.
    // Until an adapter is connected, online sessions cannot mutate unsynchronized counts.
    public void SetAuthorityCheck(Func<bool> authorityCheck) => _authorityCheck = authorityCheck;
    public bool CanExecuteLocally => !NetworkClient.active && !NetworkServer.active
        ? _authorityCheck == null || _authorityCheck()
        : NetworkServer.active && _authorityCheck != null && _authorityCheck();
    public bool CanRequestCraft(CraftingRecipeData recipe) => CanCraft(recipe) && (CanExecuteLocally || CraftRequested != null);

    public bool AlreadyOwnsUniqueResult(CraftingRecipeData recipe)
    {
        var result = recipe != null ? recipe.ResultItem : null;
        if (_inventory == null || result == null || !result.IsUnique) return false;
        if (_inventory.Contains(result)) return true;
        for (int i = 0; i < _inventory.QuickSlotCount; i++)
            if (_inventory.GetQuickSlotItem(i) == result) return true;
        return GetComponent<PlayerItemController>()?.EquippedItemData == result;
    }

    public bool CanCraft(CraftingRecipeData recipe)
    {
        if (_inventory == null || recipe == null || !PlayerActionGuard.CanAct(gameObject) || AlreadyOwnsUniqueResult(recipe)) return false;
        var costs = new Dictionary<ItemData, int>();
        return recipe.TryGetCosts(costs) && _inventory.CanExchangeItems(costs, recipe.ResultItem, recipe.ResultAmount);
    }

    public bool TryCraft(CraftingRecipeData recipe)
    {
        if (!CanExecuteLocally || !CanCraft(recipe)) return false;
        var costs = new Dictionary<ItemData, int>();
        if (!recipe.TryGetCosts(costs) || !_inventory.TryExchangeItems(costs, recipe.ResultItem, recipe.ResultAmount)) return false;
        Crafted?.Invoke(recipe);
        return true;
    }

    public bool RequestCraft(CraftingRecipeData recipe)
    {
        if (!CanRequestCraft(recipe)) return false;
        if (CanExecuteLocally) return TryCraft(recipe);
        CraftRequested?.Invoke(recipe);
        return true;
    }
}
