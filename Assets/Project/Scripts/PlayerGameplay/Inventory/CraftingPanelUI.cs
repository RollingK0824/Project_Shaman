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
    public void ConfigureNotebook()
    {
        _embedded = true;
        var background = GetComponent<Image>();
        if (background != null) background.color = Color.clear;
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.GetComponentInParent<Button>(true) == null && label.GetComponentInParent<TMP_Dropdown>(true) == null)
                label.color = new Color(.16f, .11f, .07f, 1f);
        var title = transform.Find("Title") as RectTransform;
        if (title != null) { title.anchoredPosition = new Vector2(-245f, title.anchoredPosition.y); title.sizeDelta = new Vector2(420f, 44f); }
        if (_resultIcon != null) PlayerItemUIStyle.Place(_resultIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(530, -94), new Vector2(100, 100));
        if (_resultText != null) PlayerItemUIStyle.Place(_resultText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, -110), new Vector2(294, 80));
        var ingredientsHeading = transform.Find("IngredientsHeading") as RectTransform;
        if (ingredientsHeading != null) PlayerItemUIStyle.Place(ingredientsHeading, new Vector2(0, 1), new Vector2(0, 1), new Vector2(530, -238), new Vector2(414, 38));
        var ingredients = transform.Find("Ingredients") as RectTransform;
        if (ingredients != null) PlayerItemUIStyle.Place(ingredients, new Vector2(0, 1), new Vector2(0, 1), new Vector2(530, -284), new Vector2(414, 110));
        if (_statusText != null) _statusText.rectTransform.sizeDelta = new Vector2(400, 78);
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
        _feedback = null;
        Subscribe();
        Refresh();
    }
    public void SetRecipes(IEnumerable<CraftingRecipeData> recipes)
    {
        _recipes = recipes == null ? new List<CraftingRecipeData>() : new List<CraftingRecipeData>(recipes);
        _recipes.RemoveAll(recipe => recipe == null);
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
    }
    private void Subscribe()
    {
        if (!isActiveAndEnabled || _crafting == null) return;
        _crafting.Crafted += HandleCrafted;
        _subscribedInventory = _crafting.Inventory;
        if (_subscribedInventory != null) _subscribedInventory.InventoryChanged += Refresh;
        _subscribedHealth = _crafting.GetComponent<PlayerHealth>();
        if (_subscribedHealth != null) _subscribedHealth.Died += HandleDied;
    }
    private void Unsubscribe()
    {
        if (_crafting != null) _crafting.Crafted -= HandleCrafted;
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
    public void SelectRecipe(int index) { _selectedIndex = index; _feedback = null; Refresh(); }
    public void CraftSelected()
    {
        var recipe = SelectedRecipe;
        bool valid = recipe != null && recipe.TryGetCosts(_costs);
        string reason = UnavailableReason(recipe, valid);
        if (!string.IsNullOrEmpty(reason)) SetFeedback(reason);
        else if (!_crafting.RequestCraft(recipe)) SetFeedback("제작하지 못했습니다. 재료와 현재 상태를 확인해 주세요.", true);
        // A routed network request is not a confirmed craft. Only Crafted reports success.
        else if (!_crafting.CanExecuteLocally) SetFeedback("제작 요청을 보냈습니다.", true);
        Refresh();
    }

    private void HandleCrafted(CraftingRecipeData recipe)
    {
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
        if (_embedded) PlayerItemUIStyle.FitEmbeddedPanel((RectTransform)transform);
        else PlayerItemUIStyle.FitPanel((RectTransform)transform);
        // Authority/place conditions can change without an inventory event.
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .25f;
        if (Time.unscaledTime >= _feedbackUntil) _feedback = null;
        Refresh();
    }

    private string UnavailableReason(CraftingRecipeData recipe, bool valid)
    {
        if (!valid) return "등록된 레시피가 없습니다";
        if (_crafting == null || !_crafting.isActiveAndEnabled || _crafting.Inventory == null || !PlayerActionGuard.CanAct(_crafting.gameObject))
            return "현재 상태에서는 제작할 수 없습니다.";
        var missing = new List<string>();
        foreach (var cost in _costs)
            if (_crafting.Inventory.GetItemCount(cost.Key) < cost.Value) missing.Add(cost.Key.DisplayName);
        if (missing.Count > 0) return string.Join(", ", missing) + " 부족";
        if (!_crafting.CanCraft(recipe)) return "현재 상태에서는 제작할 수 없습니다. 소지품 수량을 확인해 주세요.";
        if (!_crafting.CanRequestCraft(recipe)) return "제작 권한이 없습니다.";
        if (_crafting.CanExecuteLocally && _crafting.Inventory.SharedMaterials != null && !_crafting.Inventory.SharedMaterials.CanMutate)
            foreach (var cost in _costs)
                if (cost.Key.Category == ItemCategory.Material) return "공용 재료를 사용할 권한이 없습니다.";
        return null;
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
            {
                int owned = _crafting != null && _crafting.Inventory != null ? _crafting.Inventory.GetItemCount(cost.Key) : 0;
                bool missing = owned < cost.Value;
                if (missing) text.Append("<color=#FFA39A>");
                text.Append($"{(cost.Key.Category == ItemCategory.Material ? "[공용] " : "")}{cost.Key.DisplayName}    {owned} / {cost.Value}");
                if (missing) text.Append("  부족</color>");
                text.AppendLine();
            }
        if (_requirementsText != null) _requirementsText.text = text.ToString();
        string reason = UnavailableReason(recipe, valid);
        if (_craftButton != null) _craftButton.interactable = reason == null;
        // A stale shortage message must not contradict freshly received shared counts.
        if (!_feedbackIsResult && _feedback != reason) _feedback = null;
        if (_statusText != null)
        {
            string availability = reason ?? "제작 가능";
            _statusText.text = string.IsNullOrEmpty(_feedback) || _feedback == availability ? availability : _feedback + "\n" + availability;
        }
    }
}
