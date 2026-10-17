using UnityEngine;
using UnityEngine.Rendering;

// The world mirror owns its own texture; it never starts the first-person observation camera.
[DefaultExecutionOrder(100)]
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ThirdPersonMirrorReflection : MonoBehaviour
{
    [SerializeField] private Renderer _surface;
    private Camera _camera;
    private RenderTexture _texture;
    private MaterialPropertyBlock _originalProperties;
    private bool _observationActive;
    private static bool _rendering;
    private int _lastFrame = -1;
    private int _lastObserver;
    public RenderTexture Texture => _texture;

    public void Bind(Renderer surface) => _surface = surface;
    // The first-person live/frozen observation owns the surface until it is released.
    public void SetObservationActive(bool active)
    {
        _observationActive = active;
        if (active) ReleaseResources();
    }
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        Camera.onPreCull += BeforeBuiltinCamera;
    }
    private void BeforeCamera(ScriptableRenderContext context, Camera observer) => RenderReflection(observer);
    private void BeforeBuiltinCamera(Camera observer)
    {
        if (GraphicsSettings.currentRenderPipeline == null) RenderReflection(observer);
    }
    public void RenderReflection(Camera observer)
    {
        if (_rendering || _observationActive || !isActiveAndEnabled || !gameObject.scene.IsValid() ||
            observer == null || observer == _camera || observer.cameraType == CameraType.Reflection ||
            _surface == null || !_surface.enabled || _surface.forceRenderingOff ||
            (Application.isPlaying && _lastFrame == Time.frameCount && _lastObserver == observer.GetInstanceID())) return;
        if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(observer), _surface.bounds)) return;
        var t = _surface.transform;
        Vector3 centre = t.TransformPoint(_surface.localBounds.center);
        Vector3 normal = -t.forward;
        if (Vector3.Dot(observer.transform.position-centre, normal) <= .005f) return;
        if (_texture == null) Initialize();
        if (_camera == null) return;
        Vector3 eye = observer.transform.position;
        Vector3 reflected = eye - 2f * Vector3.Dot(eye-centre, normal) * normal;
        _camera.CopyFrom(observer);
        _camera.enabled = false; _camera.targetTexture = _texture;
        _camera.allowHDR = false; _camera.allowMSAA = false; _camera.useOcclusionCulling = false;
        _camera.transform.SetPositionAndRotation(reflected, Quaternion.LookRotation(normal, t.up));
        Vector3 c = _camera.transform.InverseTransformPoint(centre);
        float halfWidth = t.TransformVector(Vector3.right * _surface.localBounds.extents.x).magnitude;
        float halfHeight = t.TransformVector(Vector3.up * _surface.localBounds.extents.y).magnitude;
        float near = Mathf.Max(.01f, c.z + .003f);
        _camera.nearClipPlane = near; _camera.farClipPlane = Mathf.Max(near+1f, observer.farClipPlane);
        _camera.projectionMatrix = Matrix4x4.Frustum(c.x-halfWidth, c.x+halfWidth, c.y-halfHeight, c.y+halfHeight, near, _camera.farClipPlane);
        bool hidden = _surface.forceRenderingOff;
        _rendering = true;
        try
        {
            _surface.forceRenderingOff = true;
            if (GraphicsSettings.currentRenderPipeline == null) _camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = _texture };
                if (!RenderPipeline.SupportsRenderRequest(_camera, request)) return;
                RenderPipeline.SubmitRenderRequest(_camera, request);
            }
            _lastFrame = Time.frameCount;
            _lastObserver = observer.GetInstanceID();
        }
        finally { _surface.forceRenderingOff = hidden; _rendering = false; }
    }
    private void Initialize()
    {
        if (_surface.sharedMaterial == null) return;
        _originalProperties = new MaterialPropertyBlock();
        _surface.GetPropertyBlock(_originalProperties);
        _texture = new RenderTexture(512,512,24) { name="ThirdPersonMirrorRT", hideFlags=HideFlags.HideAndDontSave };
        _texture.Create();
        var properties = new MaterialPropertyBlock();
        _surface.GetPropertyBlock(properties);
        properties.SetTexture("_BaseMap", _texture);
        properties.SetTexture("_MainTex", _texture);
        _surface.SetPropertyBlock(properties);
        _camera = new GameObject("ThirdPersonMirrorCamera") { hideFlags=HideFlags.HideAndDontSave }.AddComponent<Camera>();
        _camera.enabled = false;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Camera.onPreCull -= BeforeBuiltinCamera;
        ReleaseResources();
    }
    private void ReleaseResources()
    {
        if (_surface != null && _originalProperties != null) _surface.SetPropertyBlock(_originalProperties);
        if (_texture != null) { _texture.Release(); DestroyOwned(_texture); }
        if (_camera != null) DestroyOwned(_camera.gameObject);
        _texture = null; _camera = null; _originalProperties = null;
        _lastFrame = -1;
    }
    private static void DestroyOwned(Object value)
    {
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
