using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation units are based on a 1280x720 viewport; the existing CanvasScaler remains authoritative.
public static class PlayerItemUIStyle
{
    public static readonly Color Text = new(.94f, .90f, .80f, 1f);
    public static readonly Color Muted = new(.72f, .76f, .75f, 1f);
    public static readonly Color Background = new(.075f, .09f, .11f, .92f);
    public static readonly Color NotebookInk = new(.20f, .14f, .10f, 1f);
    public static readonly Color NotebookMuted = new(.39f, .30f, .22f, 1f);
    public static readonly Color NotebookCard = new(.37f, .24f, .12f, .10f);
    public static readonly Color NotebookAccent = new(.42f, .20f, .10f, 1f);

    public static void FitNotebookSpread(RectTransform rect)
    {
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 640));
        SetScale(rect, Vector3.one * Mathf.Max(.01f, Mathf.Min(parent.rect.width / 1000f, parent.rect.height / 640f)));
    }

    public static RectTransform NotebookRect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
        return rect;
    }

    public static TMP_Text NotebookLabel(Transform parent, TMP_FontAsset font, string name, string value,
        float x, float y, float width, float height, float size = 22)
    {
        var label = NotebookRect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        Label(label, font, size); label.color = NotebookInk; label.text = value;
        label.richText = false; label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    public static Button NotebookButton(Transform parent, TMP_FontAsset font, string name, string value,
        float x, float y, float width, float height)
    {
        var rect = NotebookRect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = NotebookAccent;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.disabledColor = new Color(.6f, .6f, .6f, .5f); button.colors = colors;
        var label = NotebookLabel(rect, font, "Label", value, 8, 4, width - 16, height - 8, 20);
        label.color = Text; label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    public static RectTransform NotebookScroll(Transform parent, string name, float x, float y, float width, float height, out ScrollRect scroll)
    {
        var rect = NotebookRect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = NotebookCard;
        scroll = rect.gameObject.AddComponent<ScrollRect>();
        var viewport = NotebookRect(rect, "Viewport", 0, 0, width, height);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = NotebookRect(viewport, "Content", 0, 0, width, height);
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
        return content;
    }

    public static float ViewportUnit(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return 1f;
        // Account for ancestor scale without changing or adding a CanvasScaler.
        float parentScale = rect.parent != null ? Mathf.Abs(rect.parent.lossyScale.x) : 1f;
        return Mathf.Min(Screen.width / 1280f, Screen.height / 720f) / Mathf.Max(.001f, parentScale);
    }

    public static void FitPanel(RectTransform rect)
    {
        Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(980f, 520f));
        SetScale(rect, Vector3.one * ViewportUnit(rect));
    }

    // Notebook pages define their own available area inside the existing book artwork.
    public static void FitEmbeddedPanel(RectTransform rect)
    {
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(980f, 520f));
        SetScale(rect, Vector3.one * Mathf.Max(.01f, Mathf.Min(parent.rect.width / 980f, parent.rect.height / 520f)));
    }

    // Per-frame layout calls write only on change: transform writes dirty the whole canvas even with equal values.
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect.anchorMin != anchor) rect.anchorMin = anchor;
        if (rect.anchorMax != anchor) rect.anchorMax = anchor;
        if (rect.pivot != pivot) rect.pivot = pivot;
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
    }

    public static void SetScale(Transform transform, Vector3 scale)
    {
        if (transform.localScale != scale) transform.localScale = scale;
    }

    public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
    }

    public static void Label(TMP_Text text, TMP_FontAsset font, float size)
    {
        if (font != null) text.font = font;
        text.fontSize = size; text.enableAutoSizing = false;
        text.color = Text; text.raycastTarget = false;
    }
}
