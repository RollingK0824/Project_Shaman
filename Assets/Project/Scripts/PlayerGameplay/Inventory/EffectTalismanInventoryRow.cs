using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EffectTalismanInventoryRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Button _button;
    [SerializeField] private TalismanDrawingSurface _thumbnail;
    private Action _select;
    private void Awake() => _button.onClick.AddListener(() => _select?.Invoke());
    public void Bind(TalismanItemData item, TalismanDrawingData drawing, Action select)
    {
        _label.text = drawing.CustomName + "\n" + (item.FindEffect(drawing.EffectId)?.DisplayName ?? "알 수 없는 효과");
        _label.richText = false;
        _thumbnail.SetPaper(item.PaperTexture); _thumbnail.Bind(drawing); _thumbnail.RenderDrawing(); _select = select;
    }
}
