using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Notebook presentation only. Costs, transactions and authority stay in the existing crafting controller.
public sealed class NotebookCraftingView : MonoBehaviour
{
    private sealed class Row
    {
        public RectTransform Root;
        public Image Icon, Background;
        public TMP_Text Name, Count;
        public Button Button;
    }
    private CraftingPanelUI _panel;
    private TMP_FontAsset _font;
    private RectTransform _tools, _requirements, _materials;
    private ScrollRect _requirementScroll;
    private TMP_Text _description, _result, _status, _empty, _recipeHint;
    private Image _preview;
    private Button _craft;
    private readonly List<Row> _toolRows = new(), _costRows = new(), _materialRows = new();
    private readonly Dictionary<ItemData, int> _costs = new(), _recipeCosts = new();
    private readonly List<ItemData> _materialItems = new();
    private CraftingRecipeData _lastRecipe;
    private static readonly Color Missing = new(.65f, .17f, .12f, 1f);
    private static readonly Color Ready = new(.22f, .37f, .23f, 1f);

    public void Initialize(CraftingPanelUI panel, TMP_FontAsset font)
    {
        _panel = panel; _font = font;
        PlayerItemUIStyle.Stretch((RectTransform)transform, Vector2.zero, Vector2.zero);
        Label(transform, "ToolsHeading", "제작할 도구", 20, 16, 440, 42, 30);
        Label(transform, "ToolsHint", "도구를 선택하면 재료와 설명을 볼 수 있습니다.", 20, 62, 440, 28, 18);
        _tools = PlayerItemUIStyle.NotebookScroll(transform, "Tools", 20, 100, 440, 272, out _);
        Label(transform, "DescriptionHeading", "도구 설명", 20, 388, 440, 36, 25);
        _preview = Icon(transform, "ResultIcon", 20, 432, 64, 64);
        _result = Label(transform, "ResultName", "", 102, 432, 358, 64, 25);
        _result.textWrappingMode = TextWrappingModes.Normal;
        _description = Label(transform, "Description", "", 20, 510, 440, 90, 20);
        _description.textWrappingMode = TextWrappingModes.Normal;
        Label(transform, "ResultDestination", "완성품은 개인 소지품에 보관됩니다.", 20, 608, 440, 26, 17).color = PlayerItemUIStyle.NotebookMuted;

        Label(transform, "RecipeHeading", "레시피", 540, 16, 440, 42, 30);
        _recipeHint = Label(transform, "RecipeHint", "재료별 보유 / 필요 수량", 540, 62, 440, 28, 18);
        _requirements = PlayerItemUIStyle.NotebookScroll(transform, "Requirements", 540, 100, 440, 168, out _requirementScroll);
        Label(transform, "MaterialsHeading", "재료 소지품 · 공용", 540, 280, 440, 36, 25);
        _materials = PlayerItemUIStyle.NotebookScroll(transform, "SharedMaterials", 540, 326, 440, 234, out _);
        _empty = Label(_materials, "Empty", "보유한 공용 재료가 없습니다.", 18, 52, 404, 70, 21);
        _status = Label(transform, "CraftStatus", "", 540, 574, 270, 60, 18);
        _status.textWrappingMode = TextWrappingModes.Normal;
        _craft = PlayerItemUIStyle.NotebookButton(transform, _font, "Craft", "제작 x1", 824, 580, 156, 54);
        _craft.onClick.AddListener(_panel.CraftSelected);
    }

    private TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size)
        => PlayerItemUIStyle.NotebookLabel(parent, _font, name, value, x, y, w, h, size);
    private Image Icon(Transform parent, string name, float x, float y, float w, float h)
    {
        var icon = PlayerItemUIStyle.NotebookRect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        icon.preserveAspect = true; icon.raycastTarget = false; return icon;
    }
    private Row NewRow(RectTransform parent, string name, float width, float height)
    {
        var row = new Row { Root = PlayerItemUIStyle.NotebookRect(parent, name, 0, 0, width, height) };
        row.Background = row.Root.gameObject.AddComponent<Image>(); row.Background.color = Color.clear;
        row.Icon = Icon(row.Root, "Icon", 10, 7, 44, 44);
        row.Name = Label(row.Root, "Name", "", 66, 7, width - 78, 27, 21);
        row.Count = Label(row.Root, "Count", "", 66, 36, width - 78, 23, 17);
        return row;
    }
    private void ResizeRows(List<Row> rows, int count, RectTransform parent, string name, float height)
    {
        while (rows.Count < count) rows.Add(NewRow(parent, name + rows.Count, 440, height));
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Root.gameObject.SetActive(i < count);
            rows[i].Root.anchoredPosition = new Vector2(0, -i * height);
        }
        parent.sizeDelta = new Vector2(parent.sizeDelta.x, Mathf.Max(count * height, ((RectTransform)parent.parent).rect.height));
    }
    private static void Sprite(Image image, Sprite sprite) { image.sprite = sprite; image.enabled = sprite != null; }
    private int CraftableCount(CraftingRecipeData recipe, PlayerInventory inventory)
    {
        if (inventory == null || recipe == null || !recipe.TryGetCosts(_recipeCosts)) return 0;
        int count = int.MaxValue;
        foreach (var cost in _recipeCosts) count = Mathf.Min(count, inventory.GetItemCount(cost.Key) / cost.Value);
        return count == int.MaxValue ? 0 : count;
    }

    public void Refresh(IReadOnlyList<CraftingRecipeData> recipes, CraftingRecipeData selected,
        PlayerCraftingController crafting, string status, bool canCraft)
    {
        var inventory = crafting != null ? crafting.Inventory : null;
        ResizeRows(_toolRows, recipes.Count, _tools, "Tool", 68);
        for (int i = 0; i < recipes.Count; i++)
        {
            var row = _toolRows[i]; var recipe = recipes[i];
            if (row.Button == null)
            {
                int index = i;
                row.Button = row.Root.gameObject.AddComponent<Button>(); row.Button.targetGraphic = row.Background;
                row.Button.onClick.AddListener(() => _panel.SelectRecipe(index));
            }
            row.Background.color = recipe == selected ? new Color(.53f, .30f, .12f, .24f) : Color.clear;
            row.Name.text = recipe.ResultItem != null ? recipe.ResultItem.DisplayName : recipe.DisplayName;
            int count = CraftableCount(recipe, inventory);
            bool ownedUnique = crafting != null && crafting.AlreadyOwnsUniqueResult(recipe);
            if (ownedUnique) count = 0;
            else if (recipe.ResultItem != null && recipe.ResultItem.IsUnique) count = Mathf.Min(count, 1);
            row.Count.text = ownedUnique ? "이미 보유 중 · 추가 제작 불가" : count > 0 ? $"재료 충분 · {count}회 제작 가능" : "재료를 모아주세요";
            row.Count.color = count > 0 ? Ready : PlayerItemUIStyle.NotebookMuted;
            Sprite(row.Icon, recipe.Icon);
        }
        bool valid = selected != null && selected.TryGetCosts(_costs);
        if (!valid) _costs.Clear();
        if (_lastRecipe != selected) { _requirementScroll.verticalNormalizedPosition = 1; _lastRecipe = selected; }
        Sprite(_preview, selected != null ? selected.Icon : null);
        _result.text = valid ? $"{selected.ResultItem.DisplayName}  ×{selected.ResultAmount}" : "제작할 도구가 없습니다";
        if (valid && selected.ResultItem.IsPermanent) _result.text += "\n영구 장비";
        _description.text = selected != null ? selected.Description : "등록된 레시피가 없습니다.";
        _recipeHint.text = valid ? $"1회 제작 기준 · 결과 {selected.ResultAmount}개 · 보유 / 필요" : "레시피를 선택하세요";
        ResizeRows(_costRows, _costs.Count, _requirements, "Ingredient", 56);
        int n = 0;
        foreach (var cost in _costs)
        {
            var row = _costRows[n++]; int owned = inventory != null ? inventory.GetItemCount(cost.Key) : 0;
            row.Name.text = (cost.Key.Category == ItemCategory.Material ? "[공용] " : "[개인] ") + CraftingPanelUI.IngredientName(cost.Key);
            row.Name.fontSize = 19; row.Count.fontSize = 17;
            row.Name.rectTransform.anchoredPosition = new Vector2(66, -3);
            row.Count.rectTransform.anchoredPosition = new Vector2(66, -29);
            bool enough = owned >= cost.Value;
            row.Count.text = enough ? $"{owned} / {cost.Value}  · 준비됨" : $"{owned} / {cost.Value}  · {cost.Value - owned}개 부족";
            row.Count.color = enough ? Ready : Missing;
            Sprite(row.Icon, cost.Key.Icon);
        }
        RefreshMaterials(inventory);
        _status.text = status; _status.color = canCraft ? Ready : PlayerItemUIStyle.NotebookMuted;
        _craft.interactable = canCraft;
        _craft.GetComponentInChildren<TMP_Text>().text = "제작 x1"; // One transaction, even if its recipe yields multiple units.
    }

    private void RefreshMaterials(PlayerInventory inventory)
    {
        _materialItems.Clear();
        var store = inventory != null ? inventory.SharedMaterials : null;
        if (store != null) foreach (var pair in store.Counts) if (pair.Key != null && pair.Value > 0) _materialItems.Add(pair.Key);
        _materialItems.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
        while (_materialRows.Count < _materialItems.Count)
        {
            var row = NewRow(_materials, "Material" + _materialRows.Count, 140, 76);
            PlayerItemUIStyle.Place(row.Icon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -5), new Vector2(36, 36));
            PlayerItemUIStyle.Place(row.Name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -46), new Vector2(124, 27));
            PlayerItemUIStyle.Place(row.Count.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -8), new Vector2(82, 30));
            row.Name.fontSize = 18; row.Count.fontSize = 20; _materialRows.Add(row);
        }
        for (int i = 0; i < _materialRows.Count; i++)
        {
            var row = _materialRows[i]; row.Root.gameObject.SetActive(i < _materialItems.Count);
            if (i >= _materialItems.Count) continue;
            var item = _materialItems[i];
            row.Root.anchoredPosition = new Vector2(4 + i % 3 * 146, -(i / 3) * 78);
            row.Name.text = item.DisplayName; row.Count.text = "×" + store.GetCount(item); Sprite(row.Icon, item.Icon);
            row.Background.color = _costs.ContainsKey(item) ? new Color(.53f, .30f, .12f, .15f) : Color.clear;
        }
        _materials.sizeDelta = new Vector2(440, Mathf.Max(234, Mathf.CeilToInt(_materialItems.Count / 3f) * 78));
        _empty.gameObject.SetActive(_materialItems.Count == 0);
        _empty.text = store == null ? "공용 재료를 불러오는 중입니다." : "보유한 공용 재료가 없습니다.";
    }
}
