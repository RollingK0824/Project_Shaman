using UnityEngine;

// 마이크 아이콘 (흰색, Image.color로 색 지정). 별도 아트 없이 코드로 그림
// 음소거 버전은 사선이 그어짐
public static class MicIconSprite
{
    private const int Size = 64;

    private static Sprite _live, _muted;

    public static Sprite Get(bool muted)
    {
        if (muted)
        {
            if (_muted == null) _muted = Build(true);
            return _muted;
        }

        if (_live == null) _live = Build(false);
        return _live;
    }

    private static Sprite Build(bool muted)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = muted ? "MicIcon_Muted" : "MicIcon_Live",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        var pixels = new Color32[Size * Size];
        Vector2 slashFrom = new Vector2(12, 56), slashTo = new Vector2(52, 6);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float d = Glyph(p);

                if (muted)
                {
                    // 사선 주변을 비워서 작게 보여도 사선이 또렷하게
                    float slash = Segment(p, slashFrom, slashTo);
                    d = Mathf.Min(Mathf.Max(d, 5f - slash), slash - 2.5f);
                }

                byte alpha = (byte)(Mathf.Clamp01(.5f - d) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // 마이크 모양까지의 거리 (안쪽이면 음수)
    private static float Glyph(Vector2 p)
    {
        // 머리 (캡슐)
        float d = Segment(p, new Vector2(32, 36), new Vector2(32, 48)) - 9f;

        // 받침 U자 (아래 반원)
        float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(32, 36)) - 15f) - 1.75f;
        if (p.y <= 36f) d = Mathf.Min(d, ring);
        else d = Mathf.Min(d, Mathf.Max(Mathf.Abs(Mathf.Abs(p.x - 32f) - 15f) - 1.75f, p.y - 40f));

        // 기둥과 바닥
        d = Mathf.Min(d, Box(p, new Vector2(32, 15.5f), new Vector2(1.75f, 4.5f)));
        d = Mathf.Min(d, Box(p, new Vector2(32, 10f), new Vector2(10f, 1.75f)));
        return d;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Box(Vector2 p, Vector2 center, Vector2 half)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
    }
}
