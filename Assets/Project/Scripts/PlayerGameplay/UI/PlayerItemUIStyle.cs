using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation units are based on a 1280x720 viewport; the existing CanvasScaler remains authoritative.
public static class PlayerItemUIStyle
{
    public static readonly Color Text = new(.94f, .90f, .80f, 1f);
    public static readonly Color Muted = new(.72f, .76f, .75f, 1f);
    public static readonly Color Background = new(.075f, .09f, .11f, .92f);

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
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(980f, 520f);
        rect.localScale = Vector3.one * ViewportUnit(rect);
    }

    // Notebook pages define their own available area inside the existing book artwork.
    public static void FitEmbeddedPanel(RectTransform rect)
    {
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(980f, 520f);
        rect.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(parent.rect.width / 980f, parent.rect.height / 520f));
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = size;
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
