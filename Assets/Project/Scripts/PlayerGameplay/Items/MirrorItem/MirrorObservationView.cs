using UnityEngine;

public class MirrorObservationView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera _observationCamera;

    [SerializeField]
    private Renderer _mirrorRenderer;

    [Header("Render Texture")]
    [SerializeField]
    private int _textureWidth = 512;

    [SerializeField]
    private int _textureHeight = 512;

    [SerializeField]
    private int _depth = 16;

    private RenderTexture _renderTexture;

    private Material _runtimeMaterial;


    private void Start()
    {
        CreateRenderTexture();

        SetLive(false);
    }


    private void OnDestroy()
    {
        ReleaseRenderTexture();
    }


    public void SetLive(bool live)
    {
        if (_observationCamera == null)
        {
            return;
        }

        _observationCamera.enabled = live;
    }


    private void CreateRenderTexture()
    {
        if (_observationCamera == null ||
            _mirrorRenderer == null)
        {
            Debug.LogWarning(
                "[Mirror] Observation Camera 또는 Mirror Renderer가 연결되지 않았습니다.",
                this
            );

            return;
        }

        ReleaseRenderTexture();

        _renderTexture =
            new RenderTexture(
                _textureWidth,
                _textureHeight,
                _depth
            );

        _renderTexture.name =
            "MirrorRuntimeRT";

        _renderTexture.Create();

        _observationCamera.targetTexture =
            _renderTexture;

        _runtimeMaterial =
            new Material(
                _mirrorRenderer.material
            );

        _runtimeMaterial.mainTexture =
            _renderTexture;

        _mirrorRenderer.material =
            _runtimeMaterial;
    }


    private void ReleaseRenderTexture()
    {
        if (_observationCamera != null)
        {
            _observationCamera.targetTexture =
                null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();

            Destroy(
                _renderTexture
            );

            _renderTexture =
                null;
        }

        if (_runtimeMaterial != null)
        {
            Destroy(
                _runtimeMaterial
            );

            _runtimeMaterial =
                null;
        }
    }
}