using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SharedQuestRewardView : MonoBehaviour
{
    private SharedQuestRewardInventory _rewards;
    private PlayerInventory _recipient;
    private InventoryItemSlotUI _slotPrefab;
    private Transform _grid;
    private TMP_Text _status;
    private readonly List<InventoryItemSlotUI> _rows = new();

    public void Bind(SharedQuestRewardInventory rewards, PlayerInventory recipient, InventoryItemSlotUI slotPrefab, Action back)
    {
        if (_rewards != null) _rewards.Changed -= Refresh;
        _rewards = rewards; _recipient = recipient; _slotPrefab = slotPrefab;
        if (_grid == null) Build(back);
        if (_rewards != null) _rewards.Changed += Refresh;
        Refresh();
    }

    private TMP_Text Label(string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(transform, false);
        PlayerItemUIStyle.Place(label.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), position, size);
        PlayerItemUIStyle.Label(label, _slotPrefab.GetComponentInChildren<TMP_Text>(true).font, fontSize);
        label.text = text; label.alignment = TextAlignmentOptions.Center; label.color = new Color(.16f, .11f, .07f, 1f);
        return label;
    }

    private void Build(Action back)
    {
        Label("Title", "공용 보상함", new Vector2(-245, -16), new Vector2(420, 48), 30);
        var go = new GameObject("BackToItems", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(transform, false);
        PlayerItemUIStyle.Place((RectTransform)go.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -20), new Vector2(180, 42));
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.targetGraphic.color = new Color(.23f, .19f, .15f, 1f);
        button.onClick.AddListener(() => back());
        var caption = Label("BackLabel", "소지품", Vector2.zero, Vector2.zero, 20); caption.transform.SetParent(go.transform, false);
        PlayerItemUIStyle.Stretch(caption.rectTransform, Vector2.zero, Vector2.zero); caption.color = PlayerItemUIStyle.Text;
        var scrollObject = new GameObject("Rewards", typeof(RectTransform), typeof(Image), typeof(ScrollRect)); scrollObject.transform.SetParent(transform, false);
        scrollObject.GetComponent<Image>().color = Color.clear;
        PlayerItemUIStyle.Stretch((RectTransform)scrollObject.transform, new Vector2(30, 90), new Vector2(-30, -84));
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(scrollObject.transform, false);
        PlayerItemUIStyle.Stretch((RectTransform)viewport.transform, Vector2.zero, Vector2.zero);
        var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform, false);
        var rect = (RectTransform)content.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<GridLayoutGroup>(); layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4; layout.cellSize = new Vector2(198, 174); layout.spacing = new Vector2(32, 16); layout.padding = new RectOffset(14, 14, 8, 8);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollObject.GetComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform; scroll.content = rect; scroll.horizontal = false; scroll.scrollSensitivity = 36; scroll.movementType = ScrollRect.MovementType.Clamped;
        _grid = content.transform;
        _status = Label("Status", "", new Vector2(-245, -444), new Vector2(420, 66), 19);
        Label("ClaimHint", "보상 카드를 클릭하면 1개 수령\n완성품은 개인 소지품으로 이동합니다.", new Vector2(245, -444), new Vector2(420, 66), 18);
    }

    private void Refresh()
    {
        foreach (var row in _rows) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        _rows.Clear();
        if (_rewards == null) { _status.text = "서버 보상함 연결 대기"; return; }
        _status.text = _rewards.Counts.Count == 0 ? "보관된 공용 보상이 없습니다." : "팀이 함께 사용하는 보상입니다.";
        foreach (var reward in _rewards.Counts)
        {
            var item = reward.Key; var row = Instantiate(_slotPrefab, _grid); row.Setup(item, reward.Value);
            row.SetUse(() =>
            {
                bool claimed = _rewards.TryClaim(_recipient, item);
                _status.text = claimed ? item.DisplayName + " 1개 수령" : "수령할 수 없습니다. 수량과 권한을 확인하세요.";
            });
            _rows.Add(row);
        }
    }

    private void LateUpdate() => PlayerItemUIStyle.FitEmbeddedPanel((RectTransform)transform);
    private void OnDestroy() { if (_rewards != null) _rewards.Changed -= Refresh; }
}
