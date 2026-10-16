using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MirrorObservationView : MonoBehaviour
{
    [SerializeField] private Camera _observationCamera;
    [SerializeField] private Renderer _mirrorRenderer;
    [SerializeField, Min(32)] private int _textureWidth = 512;
    [SerializeField, Min(32)] private int _textureHeight = 512;
    [SerializeField] private int _depth = 16;
    private RenderTexture _renderTexture;
    private Material _runtimeMaterial, _originalMaterial;
    private GameObject _owner;
    private readonly Dictionary<Renderer, bool> _hidden = new();
    public bool IsLive => _observationCamera != null && _observationCamera.enabled;
    public RenderTexture Texture => _renderTexture;
    public void BindOwner(GameObject owner) => _owner = owner;
    private void Awake() { if (_observationCamera != null) _observationCamera.enabled = false; }
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeRender;
        RenderPipelineManager.endCameraRendering += AfterRender;
        Camera.onPreCull += BeforeCamera;
        Camera.onPostRender += AfterCamera;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeRender;
        RenderPipelineManager.endCameraRendering -= AfterRender;
        Camera.onPreCull -= BeforeCamera;
        Camera.onPostRender -= AfterCamera;
        Release();
    }
    private void OnDestroy() => Release();
    public void SetLive(bool live)
    {
        if (_observationCamera == null) return;
        if (live && (!isActiveAndEnabled || !EnsureTexture())) return;
        _observationCamera.enabled = live;
    }
    private bool EnsureTexture()
    {
        if (_renderTexture != null) return true;
        if (_mirrorRenderer == null || _observationCamera == null || _mirrorRenderer.sharedMaterial == null) return false;
        _originalMaterial = _mirrorRenderer.sharedMaterial;
        _renderTexture = new RenderTexture(Mathf.Clamp(_textureWidth,32,2048), Mathf.Clamp(_textureHeight,32,2048), _depth);
        _renderTexture.name = "MirrorRuntimeRT";
        _renderTexture.Create();
        var previous = RenderTexture.active;
        RenderTexture.active = _renderTexture; GL.Clear(true,true,Color.black); RenderTexture.active = previous;
        // sharedMaterial avoids creating an extra implicit material instance.
        _runtimeMaterial = new Material(_originalMaterial) { name = "MirrorRuntimeMaterial" };
        _runtimeMaterial.mainTexture = _renderTexture;
        _mirrorRenderer.sharedMaterial = _runtimeMaterial;
        _observationCamera.targetTexture = _renderTexture;
        return true;
    }
    public void Release()
    {
        RestoreRenderers();
        if (_observationCamera != null) { _observationCamera.enabled = false; _observationCamera.targetTexture = null; }
        if (_mirrorRenderer != null && _runtimeMaterial != null) _mirrorRenderer.sharedMaterial = _originalMaterial;
        if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); _renderTexture = null; }
        if (_runtimeMaterial != null) { Destroy(_runtimeMaterial); _runtimeMaterial = null; }
    }
    private void BeforeRender(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);
    private void AfterRender(ScriptableRenderContext context, Camera camera) => AfterCamera(camera);
    private void BeforeCamera(Camera camera)
    {
        if (camera != _observationCamera || _hidden.Count != 0) return;
        // Per-camera exclusion: do not change shared Player layers or hide other players.
        foreach (var renderer in (_owner != null ? _owner : gameObject).GetComponentsInChildren<Renderer>(true))
        { _hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
        if (_mirrorRenderer != null && !_hidden.ContainsKey(_mirrorRenderer))
        { _hidden[_mirrorRenderer] = _mirrorRenderer.forceRenderingOff; _mirrorRenderer.forceRenderingOff = true; }
    }
    private void AfterCamera(Camera camera) { if (camera == _observationCamera) RestoreRenderers(); }
    private void RestoreRenderers()
    {
        foreach (var pair in _hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        _hidden.Clear();
    }
}
