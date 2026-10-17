using UnityEngine;

// Presentation only: fit mesh geometry in the lower-right viewport, above the HUD strip.
// The child keeps its own use-animation transform; this parent changes only on layout changes.
[DefaultExecutionOrder(200)]
public sealed class FirstPersonItemLayout : MonoBehaviour
{
    private const float Right = .955f;
    private const float Bottom = .18f;
    private const float MaxWidth = .33f;
    private Camera _camera;
    private ItemData _data;
    private Renderer[] _renderers;
    private Rect _pixelRect;
    private float _fov, _aspect, _orthographicSize;
    private bool _fitted, _orthographic;

    public void Bind(Camera camera, ItemData data, Renderer[] renderers)
    {
        _camera = camera; _data = data; _renderers = renderers;
        _fitted = false;
        Fit();
    }

    private void LateUpdate()
    {
        if (_camera == null || _data == null) return;
        if (!_fitted || _pixelRect != _camera.pixelRect || _fov != _camera.fieldOfView ||
            _aspect != _camera.aspect || _orthographic != _camera.orthographic ||
            _orthographicSize != _camera.orthographicSize) Fit();
    }

    private void Fit()
    {
        if (_camera == null || _data == null || _renderers == null) return;
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        float preferredHeight = Mathf.Clamp(_data.FirstPersonViewportHeight, .08f, .52f);
        // Perspective means scaling also changes depth. Re-measure after every adjustment.
        for (int pass = 0; pass < 8; pass++)
        {
            if (!TryGetViewportBounds(out Rect rect)) return;
            float scale = Mathf.Min(preferredHeight / Mathf.Max(.0001f, rect.height),
                MaxWidth / Mathf.Max(.0001f, rect.width));
            transform.localScale *= Mathf.Clamp(scale, .5f, 2f);
            if (!TryGetViewportBounds(out rect)) return;
            float depth = _camera.WorldToViewportPoint(transform.position).z;
            var desired = new Vector3(Right - rect.width * .5f, Bottom + rect.height * .5f, depth);
            var current = new Vector3(rect.center.x, rect.center.y, depth);
            transform.position += _camera.ViewportToWorldPoint(desired) - _camera.ViewportToWorldPoint(current);
        }
        _pixelRect = _camera.pixelRect; _fov = _camera.fieldOfView; _aspect = _camera.aspect;
        _orthographic = _camera.orthographic; _orthographicSize = _camera.orthographicSize;
        _fitted = true;
    }

    public bool TryGetViewportBounds(out Rect rect)
    {
        rect = default;
        if (_camera == null || _renderers == null) return false;
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (var renderer in _renderers)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
            var bounds = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = _camera.WorldToViewportPoint(renderer.localToWorldMatrix.MultiplyPoint3x4(corner));
                if (point.z <= _camera.nearClipPlane) return false;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point); found = true;
            }
        }
        if (found) rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return found;
    }
}
