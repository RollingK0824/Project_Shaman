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
    [SerializeField, Min(1f)] private float _feedbackDuration = 4f;
    private bool _embedded;
    private NotebookCraftingView _notebookView;
    private CraftingRecipeData _pendingRecipe;
    public bool HasPendingRequest => _pendingRecipe != null;
    public void ConfigureNotebook()
    {
        _embedded = true;
        if (_notebookView != null) return;
        var font = GetComponentInChildren<TMP_Text>(true)?.font;
        var background = GetComponent<Image>();
        if (background != null) background.color = Color.clear;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var view = new GameObject("NotebookCrafting", typeof(RectTransform), typeof(NotebookCraftingView));
        view.transform.SetParent(transform, false);
        _notebookView = view.GetComponent<NotebookCraftingView>();
        _notebookView.Initialize(this, font);
        Refresh();
    }
    private readonly Dictionary<ItemData, int> _costs = new();
    private PlayerInventory _subscribedInventory;
    private PlayerHealth _subscribedHealth;
    private int _selectedIndex;
    private string _feedback;
    private bool _feedbackIsResult;
    private float _feedbackUntil, _nextRefresh;
    public CraftingRecipeData SelectedRecipe => _selectedIndex >= 0 && _selectedIndex < _recipes.Count ? _recipes[_selectedIndex] : null;

    public void Bind(PlayerCraftingController crafting)
    {
        Unsubscribe();
        _crafting = crafting;
        _pendingRecipe = null;
        _feedback = null;
        Subscribe();
        Refresh();
    }
    public void SetRecipes(IEnumerable<CraftingRecipeData> recipes)
    {
        _recipes = recipes == null ? new List<CraftingRecipeData>() : new List<CraftingRecipeData>(recipes);
        _recipes.RemoveAll(recipe => recipe == null || !recipe.IsAvailable);
        _selectedIndex = 0;
        _feedback = null;
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
        // A response can arrive after the user closes the notebook or switches tabs.
        if (_pendingRecipe != null && _crafting != null)
        {
            _crafting.Crafted += HandleCrafted;
            _crafting.CraftRequestFailed += HandleCraftRequestFailed;
        }
    }
    private void OnDestroy() => Unsubscribe();
    private void Subscribe()
    {
        Unsubscribe();
        if (!isActiveAndEnabled || _crafting == null) return;
        _crafting.Crafted += HandleCrafted;
        _crafting.CraftRequestFailed += HandleCraftRequestFailed;
        _subscribedInventory = _crafting.Inventory;
        if (_subscribedInventory != null) _subscribedInventory.InventoryChanged += Refresh;
        _subscribedHealth = _crafting.GetComponent<PlayerHealth>();
        if (_subscribedHealth != null) _subscribedHealth.Died += HandleDied;
    }
    private void Unsubscribe()
    {
        if (_crafting != null)
        {
            _crafting.Crafted -= HandleCrafted;
            _crafting.CraftRequestFailed -= HandleCraftRequestFailed;
        }
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
    public void SelectRecipe(int index) { _selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _recipes.Count - 1)); _feedback = null; Refresh(); }
    public void CraftSelected()
    {
        var recipe = SelectedRecipe;
        bool valid = recipe != null && recipe.TryGetCosts(_costs);
        string reason = UnavailableReason(recipe, valid);
        if (!string.IsNullOrEmpty(reason)) SetFeedback(reason);
        else
        {
            // Mark pending before dispatch: an adapter may confirm synchronously.
            if (!_crafting.CanExecuteLocally) _pendingRecipe = recipe;
            if (!_crafting.RequestCraft(recipe))
            {
                _pendingRecipe = null;
                SetFeedback("제작하지 못했습니다. 재료와 현재 상태를 확인해 주세요.", true);
            }
            else if (_pendingRecipe != null) SetFeedback("제작 중입니다. 잠시 기다려 주세요.", true);
        }
        Refresh();
    }

    // The existing network adapter can report a rejected request without faking a craft success.
    private void HandleCraftRequestFailed() => NotifyCraftRequestFailed(null);
    public void NotifyCraftRequestFailed(string message)
    {
        _pendingRecipe = null;
        if (!isActiveAndEnabled) Unsubscribe();
        SetFeedback(string.IsNullOrWhiteSpace(message) ? "제작하지 못했습니다. 다시 시도해 주세요." : message, true);
        Refresh();
    }

    private void HandleCrafted(CraftingRecipeData recipe)
    {
        if (_pendingRecipe == recipe) _pendingRecipe = null;
        if (!isActiveAndEnabled && _pendingRecipe == null) Unsubscribe();
        SetFeedback($"{recipe.ResultItem.DisplayName} 제작 완료 · x{recipe.ResultAmount}", true);
        Refresh();
    }
    private void SetFeedback(string text, bool result = false)
    {
        _feedback = text; _feedbackIsResult = result;
        _feedbackUntil = Time.unscaledTime + _feedbackDuration;
    }

    private void LateUpdate()
    {
        if (_embedded) PlayerItemUIStyle.FitNotebookSpread((RectTransform)transform);
        else PlayerItemUIStyle.FitPanel((RectTransform)transform);
        // Authority/place conditions can change without an inventory event.
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .25f;
        if (Time.unscaledTime >= _feedbackUntil) _feedback = null;
        Refresh();
    }

    private string UnavailableReason(CraftingRecipeData recipe, bool valid)
    {
        if (_pendingRecipe != null) return "제작 중입니다. 잠시 기다려 주세요.";
        if (!valid) return "등록된 레시피가 없습니다";
        if (_crafting == null || !_crafting.isActiveAndEnabled || _crafting.Inventory == null || !PlayerActionGuard.CanAct(_crafting.gameObject))
            return "현재 상태에서는 제작할 수 없습니다.";
        if (_crafting.AlreadyOwnsUniqueResult(recipe)) return $"이미 {recipe.ResultItem.DisplayName}을 보유하고 있습니다.";
        var missing = new List<string>();
        foreach (var cost in _costs)
            if (_crafting.Inventory.GetItemCount(cost.Key) < cost.Value) missing.Add(IngredientName(cost.Key));
        if (missing.Count > 0) return string.Join(", ", missing) + " 재료가 부족합니다.";
        if (!_crafting.CanCraft(recipe)) return "현재 상태에서는 제작할 수 없습니다. 소지품 수량을 확인해 주세요.";
        if (!_crafting.CanRequestCraft(recipe)) return "제작 권한이 없습니다.";
        if (_crafting.CanExecuteLocally && _crafting.Inventory.SharedMaterials != null && !_crafting.Inventory.SharedMaterials.CanMutate)
            foreach (var cost in _costs)
                if (cost.Key.Category == ItemCategory.Material) return "공용 재료를 사용할 권한이 없습니다.";
        return null;
    }

    public static string IngredientName(ItemData item) => item is TalismanItemData talisman && talisman.IsEffectTalisman
        ? "미사용 " + item.DisplayName : item.DisplayName;

    public void Refresh()
    {
        var recipe = SelectedRecipe;
        bool valid = recipe != null && recipe.TryGetCosts(_costs);
        if (_resultIcon != null) { _resultIcon.sprite = recipe != null ? recipe.Icon : null; _resultIcon.enabled = _resultIcon.sprite != null; }
        if (_resultText != null) _resultText.text = valid ? $"{recipe.ResultItem.DisplayName}  x{recipe.ResultAmount}" : "레시피를 선택하세요";
        if (!valid) _costs.Clear();
        var text = new StringBuilder();
        if (valid)
            foreach (var cost in _costs)
            {
                int owned = _crafting != null && _crafting.Inventory != null ? _crafting.Inventory.GetItemCount(cost.Key) : 0;
                bool missing = owned < cost.Value;
                if (missing) text.Append("<color=#FFA39A>");
                text.Append($"{(cost.Key.Category == ItemCategory.Material ? "[공용] " : "[개인] ")}{IngredientName(cost.Key)}    {owned} / {cost.Value}");
                if (missing) text.Append("  부족</color>");
                text.AppendLine();
            }
        if (_requirementsText != null) _requirementsText.text = text.ToString();
        string reason = UnavailableReason(recipe, valid);
        if (_craftButton != null) _craftButton.interactable = reason == null;
        // A stale shortage message must not contradict freshly received shared counts.
        if (!_feedbackIsResult && _feedback != reason) _feedback = null;
        string availability = reason ?? "재료가 준비되었습니다.";
        string status = string.IsNullOrEmpty(_feedback) || _feedback == availability ? availability : _feedback + "\n" + availability;
        if (_statusText != null) _statusText.text = status;
        if (_notebookView != null) _notebookView.Refresh(_recipes, recipe, _crafting, status, reason == null);
    }
}
