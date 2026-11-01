using UnityEngine;
using UnityEngine.UI;

// Ink silhouettes and white brush marks, matching the compass/day HUD.
// Shared artwork is referenced, never modified; geometry rebuilds only on UI changes.
public sealed class PlayerHUDFrame : MaskableGraphic
{
    private bool _selected;
    private bool _bookIcon;
    private Image _wash, _selection;
    public bool BookIcon
    {
        get => _bookIcon;
        set { if (_bookIcon == value) return; _bookIcon = value; RefreshArt(); SetVerticesDirty(); }
    }
    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        RefreshArt(); SetVerticesDirty();
    }
    private void RefreshArt()
    {
        if (_wash != null) _wash.enabled = _bookIcon || rectTransform.rect.width > 150;
        if (_selection != null) _selection.enabled = _selected && !_bookIcon;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 16 || r.height < 16) return;
        Color ink = PlayerHUDArt.Text;
        if (_bookIcon)
        {
            Vector2 c = r.center + Vector2.up * 10;
            // Open, uneven white ring echoes the painted zodiac seal without copying an animal.
            for (int i = 0; i < 48; i++)
            {
                float a = (i * 7f + 12f) * Mathf.Deg2Rad, b = ((i + 1) * 7f + 12f) * Mathf.Deg2Rad;
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (22f + Mathf.Sin(i * 1.7f) * .65f);
                Vector2 q = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (22f + Mathf.Sin((i + 1) * 1.7f) * .65f);
                Line(vh, p, q, 1.5f + .8f * Mathf.Sin(i * .23f + 1), ink);
            }
            Line(vh, c + new Vector2(0, -10), c + new Vector2(0, 11), 1.4f, ink);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 a = c + new Vector2(side * 12, 13), b = c + new Vector2(side * 12, -8);
                Line(vh, c + Vector2.up * 11, a, 1.6f, ink); Line(vh, a, b, 1.2f, ink);
                Line(vh, b, c + Vector2.down * 10, 1.6f, ink);
                for (int i = 0; i < 3; i++)
                    Line(vh, c + new Vector2(side * 4, 6 - i * 5), c + new Vector2(side * 9, 7 - i * 5), .7f, ink);
            }
        }
        else if (r.width < 150)
        {
            // Compass-like ticks keep empty slots legible without six rectangular boxes.
            var c = new Vector2(r.center.x, r.yMin + 4);
            if (_selected)
            {
                Line(vh, c + new Vector2(-4, 0), c + new Vector2(0, 3), 1.5f, ink);
                Line(vh, c + new Vector2(0, 3), c + new Vector2(4, 0), 1.5f, ink);
            }
            else Line(vh, c, c + Vector2.up * 3, 1, new Color(1, 1, 1, .5f));
        }
    }
    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
        int i = vh.currentVertCount;
        vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
        vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
    private static Image Brush(RectTransform parent, string name, Sprite sprite)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.transform.SetAsFirstSibling();
        image.sprite = sprite; image.raycastTarget = false;
        return image;
    }
    public static PlayerHUDFrame Attach(RectTransform parent)
    {
        var existing = parent.Find("ShamanFrame");
        if (existing != null) return existing.GetComponent<PlayerHUDFrame>();
        var frame = new GameObject("ShamanFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(PlayerHUDFrame)).GetComponent<PlayerHUDFrame>();
        frame.transform.SetParent(parent, false); frame.transform.SetAsFirstSibling();
        PlayerItemUIStyle.Stretch(frame.rectTransform, Vector2.one, -Vector2.one);
        frame.raycastTarget = false;
        var art = PlayerHUDArt.Current;
        if (art != null)
        {
            frame._selection = Brush(parent, "SelectedInkStroke", art.RedBrush);
            PlayerItemUIStyle.Place(frame._selection.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 5), new Vector2(64, 22));
            frame._selection.color = new Color(1, 1, 1, .9f);
            frame._wash = Brush(parent, "InkWash", art.InkWash);
            PlayerItemUIStyle.Stretch(frame._wash.rectTransform, Vector2.zero, Vector2.zero);
            frame._wash.color = new Color(1, 1, 1, .7f);
        }
        frame.RefreshArt();
        return frame;
    }
}
