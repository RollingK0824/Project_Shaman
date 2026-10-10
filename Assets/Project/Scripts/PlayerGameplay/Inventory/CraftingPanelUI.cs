using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingPanelUI : MonoBehaviour
{
    [SerializeField] private PlayerCraftingController _crafting;
    [SerializeField] private List<CraftingRecipeData> _recipes = new();
    [SerializeField] private TMP_Dropdown _recipeSelector;
    [SerializeField] private Image _resultIcon;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private TMP_Text _requirementsText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button _craftButton;
    private readonly Dictionary<ItemData, int> _costs = new();
    private PlayerInventory _subscribedInventory;
    private PlayerHealth _subscribedHealth;
    private int _selectedIndex;
    public CraftingRecipeData SelectedRecipe => _selectedIndex >= 0 && _selectedIndex < _recipes.Count ? _recipes[_selectedIndex] : null;

    public void Bind(PlayerCraftingController crafting)
    {
        Unsubscribe();
        _crafting = crafting;
        Subscribe();
        Refresh();
    }
    public void SetRecipes(IEnumerable<CraftingRecipeData> recipes)
    {
        _recipes = recipes == null ? new List<CraftingRecipeData>() : new List<CraftingRecipeData>(recipes);
        _recipes.RemoveAll(recipe => recipe == null);
        _selectedIndex = 0;
        RefreshSelector();
        Refresh();
    }
    private void OnEnable()
    {
        if (_craftButton != null) _craftButton.onClick.AddListener(CraftSelected);
        if (_recipeSelector != null) _recipeSelector.onValueChanged.AddListener(SelectRecipe);
        Subscribe();
        RefreshSelector();
        Refresh();
    }
    private void OnDisable()
    {
        if (_craftButton != null) _craftButton.onClick.RemoveListener(CraftSelected);
        if (_recipeSelector != null) _recipeSelector.onValueChanged.RemoveListener(SelectRecipe);
        Unsubscribe();
    }
    private void Subscribe()
    {
        if (!isActiveAndEnabled || _crafting == null) return;
        _subscribedInventory = _crafting.Inventory;
        if (_subscribedInventory != null) _subscribedInventory.InventoryChanged += Refresh;
        _subscribedHealth = _crafting.GetComponent<PlayerHealth>();
        if (_subscribedHealth != null) _subscribedHealth.Died += HandleDied;
    }
    private void Unsubscribe()
    {
        if (_subscribedInventory != null) _subscribedInventory.InventoryChanged -= Refresh;
        _subscribedInventory = null;
        if (_subscribedHealth != null) _subscribedHealth.Died -= HandleDied;
        _subscribedHealth = null;
    }
    private void HandleDied(GameObject source) => Refresh();
    private void RefreshSelector()
    {
        if (_recipeSelector == null) return;
        _recipeSelector.ClearOptions();
        var options = new List<string>();
        foreach (var recipe in _recipes) options.Add(recipe != null ? recipe.DisplayName : "레시피 없음");
        _recipeSelector.AddOptions(options);
        _recipeSelector.SetValueWithoutNotify(Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _recipes.Count - 1)));
        _recipeSelector.interactable = options.Count > 0;
    }
    public void SelectRecipe(int index) { _selectedIndex = index; Refresh(); }
    public void CraftSelected()
    {
        if (_crafting != null) _crafting.RequestCraft(SelectedRecipe);
        Refresh();
    }
    public void Refresh()
    {
        var recipe = SelectedRecipe;
        bool valid = recipe != null && recipe.TryGetCosts(_costs);
        if (_resultIcon != null) { _resultIcon.sprite = recipe != null ? recipe.Icon : null; _resultIcon.enabled = _resultIcon.sprite != null; }
        if (_resultText != null) _resultText.text = valid ? $"{recipe.ResultItem.DisplayName}  x{recipe.ResultAmount}" : "레시피를 선택하세요";
        var text = new StringBuilder();
        if (valid)
            foreach (var cost in _costs)
                text.AppendLine($"{cost.Key.DisplayName}    {(_crafting != null && _crafting.Inventory != null ? _crafting.Inventory.GetItemCount(cost.Key) : 0)} / {cost.Value}");
        if (_requirementsText != null) _requirementsText.text = text.ToString();
        bool canCraft = valid && _crafting != null && _crafting.CanRequestCraft(recipe);
        if (_craftButton != null) _craftButton.interactable = canCraft;
        if (_statusText != null) _statusText.text = !valid ? "등록된 레시피가 없습니다" : canCraft ? "제작 가능" : _crafting != null && !_crafting.CanCraft(recipe) ? "재료가 부족하거나 제작할 수 없는 상태입니다" : "지금은 제작할 수 없습니다";
    }
}
