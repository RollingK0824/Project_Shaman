using UnityEngine;
using UnityEngine.UI;

// Texture projection only. The drawing data owns strokes; this component owns/disposes GPU resources.
public class TalismanDrawingSurface : MonoBehaviour, IItemVisualContext
{
    [SerializeField] private Renderer _paperRenderer;
    [SerializeField] private RawImage _paperImage;
    [SerializeField] private Texture2D _paperTexture;
    [SerializeField] private bool _inkOnly;
    [SerializeField] private Rect _drawingRegion = new Rect(.23f, .07f, .53f, .86f);
    [SerializeField] private Color _paperColor = new Color(.94f, .83f, .55f, 1f);
    [SerializeField] private Color _inkColor = new Color(.52f, .06f, .035f, 1f);
    private const int Width = 256, Height = 512;
    private Texture2D _texture;
    private Color32[] _pixels;
    private Color32[] _background;
    private bool _dirty = true;
    private TalismanDrawingData _data;
    private PlayerTalismanCollection _collection;
    private TalismanItemData _item;
    public Texture2D DrawingTexture => _texture;
    public void BindItemVisual(ItemData item, GameObject owner)
    {
        if (_collection != null) _collection.SelectionChanged -= RefreshSelection;
        _collection = owner != null ? owner.GetComponent<PlayerTalismanCollection>() : null;
        _item = item as TalismanItemData;
        if (_item != null) SetPaper(_item.PaperTexture);
        if (_collection != null) _collection.SelectionChanged += RefreshSelection;
        RefreshSelection();
    }
    private void RefreshSelection() => Bind(_collection != null ? _collection.GetSelected(_item) : null);
    public void Bind(TalismanDrawingData data)
    {
        if (_data != null) _data.Changed -= MarkDirty;
        _data = data;
        if (_data != null) _data.Changed += MarkDirty;
        MarkDirty();
    }
    private void MarkDirty() => _dirty = true;
    public void SetPaper(Texture2D paper) { _paperTexture = paper; _background = null; MarkDirty(); }
    private void LateUpdate() { if (_dirty) RenderDrawing(); }
    public void RenderDrawing()
    {
        _dirty = false;
        if (_texture == null)
        {
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = "Talisman Instance Drawing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _pixels = new Color32[Width * Height];
            if (_paperImage != null) _paperImage.texture = _texture;
            if (_paperRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                _paperRenderer.GetPropertyBlock(block);
                if (_inkOnly) block.SetTexture("_DrawingInk", _texture);
                else { block.SetTexture("_BaseMap", _texture); block.SetTexture("_MainTex", _texture); }
                _paperRenderer.SetPropertyBlock(block);
            }
        }
        if (_background == null)
        {
            _background = new Color32[Width * Height];
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
                _background[y * Width + x] = _inkOnly ? Color.clear : _paperTexture != null && _paperTexture.isReadable ? _paperTexture.GetPixelBilinear(x / (float)(Width - 1), y / (float)(Height - 1)) : _paperColor;
        }
        System.Array.Copy(_background, _pixels, _pixels.Length);
        if (_data != null)
            foreach (var stroke in _data.Strokes)
                for (int i = 0; i < stroke.Points.Count; i++)
                    Line(stroke.Points[Mathf.Max(0, i - 1)], stroke.Points[i], stroke.Eraser);
        _texture.SetPixels32(_pixels); _texture.Apply(false, false);
    }
    private void Line(Vector2 a, Vector2 b, bool eraser)
    {
        a = _drawingRegion.min + Vector2.Scale(a, _drawingRegion.size);
        b = _drawingRegion.min + Vector2.Scale(b, _drawingRegion.size);
        var p = new Vector2(a.x * (Width - 1), a.y * (Height - 1));
        var q = new Vector2(b.x * (Width - 1), b.y * (Height - 1));
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p, q)));
        for (int i = 0; i <= steps; i++)
        {
            var v = Vector2.Lerp(p, q, i / (float)steps);
            int x = Mathf.RoundToInt(v.x), y = Mathf.RoundToInt(v.y);
            int radius = eraser ? 6 : 1;
            for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                if (dx * dx + dy * dy <= radius * radius && x + dx >= 0 && x + dx < Width && y + dy >= 0 && y + dy < Height &&
                    _drawingRegion.Contains(new Vector2((x + dx) / (float)(Width - 1), (y + dy) / (float)(Height - 1))))
                {
                    int index = (y + dy) * Width + x + dx;
                    _pixels[index] = eraser ? _background[index] : (Color32)_inkColor;
                }
        }
    }
    private void OnDestroy()
    {
        if (_collection != null) _collection.SelectionChanged -= RefreshSelection;
        if (_data != null) _data.Changed -= MarkDirty;
        if (_texture != null) Destroy(_texture);
    }
}
