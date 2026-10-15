using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CraftingIngredient
{
    public ItemData Item;
    [Min(1)] public int Amount;
}

[CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Project Shaman/Crafting Recipe")]
public class CraftingRecipeData : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private CraftingIngredient[] _ingredients = Array.Empty<CraftingIngredient>();
    [SerializeField] private ItemData _resultItem;
    [SerializeField, Min(1)] private int _resultAmount = 1;

    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) && _resultItem != null ? _resultItem.DisplayName : _displayName;
    public Sprite Icon => _icon != null ? _icon : _resultItem != null ? _resultItem.Icon : null;
    public IReadOnlyList<CraftingIngredient> Ingredients => _ingredients;
    public ItemData ResultItem => _resultItem;
    public int ResultAmount => _resultAmount;

    // Aggregate repeated ingredient rows before checking quantities.
    public bool TryGetCosts(Dictionary<ItemData, int> costs)
    {
        costs.Clear();
        if (_resultItem == null || _resultAmount <= 0 || _ingredients == null || _ingredients.Length == 0) return false;
        foreach (var ingredient in _ingredients)
        {
            if (ingredient.Item == null || ingredient.Amount <= 0) { costs.Clear(); return false; }
            costs.TryGetValue(ingredient.Item, out int current);
            long total = (long)current + ingredient.Amount;
            if (total > int.MaxValue) { costs.Clear(); return false; }
            costs[ingredient.Item] = (int)total;
        }
        return true;
    }
}
