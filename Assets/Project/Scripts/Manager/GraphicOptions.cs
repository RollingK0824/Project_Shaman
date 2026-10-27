using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum AntiAliasingOption
{
    Off,
    FXAA,
    SMAA,
    MSAA2x,
    MSAA4x
}

public enum ShadowQualityOption
{
    Off,
    Low,
    Medium,
    High
}

public static class GraphicOptions
{
    private const string SSAO_KEY = "SSAO";
    private const string ANTI_ALIASING_KEY = "AntiAliasing";
    private const string SHADOW_KEY = "ShadowQuality";
    private const string BRIGHTNESS_KEY = "Brightness";
    private const string CONTRAST_KEY = "Contrast";

    public const float MIN_BRIGHTNESS = -1f;
    public const float MAX_BRIGHTNESS = 1f;
    public const float MIN_CONTRAST = -50f;
    public const float MAX_CONTRAST = 50f;

    public static readonly string[] AntiAliasingLabels = { "끔", "FXAA", "SMAA", "MSAA 2x", "MSAA 4x" };
    public static readonly string[] ShadowLabels = { "끔", "낮음", "중간", "높음" };

    private static AntiAliasingOption? _antiAliasing;
    private static ColorAdjustments _colorAdjustments;

    private static bool _savedOriginals;
    private static bool _originalSsao;
    private static int _originalMsaa;
    private static float _originalShadowDistance;
    private static int _originalShadowResolution;
    private static int _originalShadowCascades;


    public static bool Ssao => PlayerPrefs.GetInt(SSAO_KEY, 1) == 1;

    public static void SetSsao(bool on)
    {
        PlayerPrefs.SetInt(SSAO_KEY, on ? 1 : 0);
        ApplySsao(on);
    }

    private static void ApplySsao(bool on)
    {
        ScreenSpaceAmbientOcclusion ssao = FindSsao();
        if (ssao != null)
        {
            ssao.SetActive(on);
        }
    }

    private static ScreenSpaceAmbientOcclusion FindSsao()
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset == null)
        {
            return null;
        }

        foreach (ScriptableRendererData data in asset.rendererDataList)
        {
            if (data == null)
            {
                continue;
            }

            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion ssao)
                {
                    return ssao;
                }
            }
        }
        return null;
    }


    public static AntiAliasingOption AntiAliasing
    {
        get
        {
            if (_antiAliasing == null)
            {
                _antiAliasing = (AntiAliasingOption)PlayerPrefs.GetInt(ANTI_ALIASING_KEY, (int)AntiAliasingOption.Off);
            }
            return _antiAliasing.Value;
        }
    }

    public static void SetAntiAliasing(AntiAliasingOption option)
    {
        _antiAliasing = option;
        PlayerPrefs.SetInt(ANTI_ALIASING_KEY, (int)option);
        ApplyMsaa(option);
    }

    private static void ApplyMsaa(AntiAliasingOption option)
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset == null)
        {
            return;
        }

        asset.msaaSampleCount = option switch
        {
            AntiAliasingOption.MSAA2x => 2,
            AntiAliasingOption.MSAA4x => 4,
            _ => 1
        };
    }

    private static AntialiasingMode ToCameraMode(AntiAliasingOption option)
    {
        return option switch
        {
            AntiAliasingOption.FXAA => AntialiasingMode.FastApproximateAntialiasing,
            AntiAliasingOption.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            _ => AntialiasingMode.None
        };
    }


    public static ShadowQualityOption ShadowQuality =>
        (ShadowQualityOption)PlayerPrefs.GetInt(SHADOW_KEY, (int)ShadowQualityOption.High);

    public static void SetShadowQuality(ShadowQualityOption option)
    {
        PlayerPrefs.SetInt(SHADOW_KEY, (int)option);
        ApplyShadows(option);
    }

    private static void ApplyShadows(ShadowQualityOption option)
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset == null)
        {
            return;
        }

        switch (option)
        {
            case ShadowQualityOption.Off:
                asset.shadowDistance = 0f;
                break;
            case ShadowQualityOption.Low:
                asset.shadowDistance = 25f;
                asset.mainLightShadowmapResolution = 1024;
                asset.shadowCascadeCount = 1;
                break;
            case ShadowQualityOption.Medium:
                asset.shadowDistance = 40f;
                asset.mainLightShadowmapResolution = 2048;
                asset.shadowCascadeCount = 2;
                break;
            default:
                asset.shadowDistance = 50f;
                asset.mainLightShadowmapResolution = 2048;
                asset.shadowCascadeCount = 4;
                break;
        }
    }


    public static float Brightness => PlayerPrefs.GetFloat(BRIGHTNESS_KEY, 0f);

    public static void SetBrightness(float v)
    {
        v = Mathf.Clamp(v, MIN_BRIGHTNESS, MAX_BRIGHTNESS);
        PlayerPrefs.SetFloat(BRIGHTNESS_KEY, v);
        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.value = v;
        }
    }

    public static float Contrast => PlayerPrefs.GetFloat(CONTRAST_KEY, 0f);

    public static void SetContrast(float v)
    {
        v = Mathf.Clamp(v, MIN_CONTRAST, MAX_CONTRAST);
        PlayerPrefs.SetFloat(CONTRAST_KEY, v);
        if (_colorAdjustments != null)
        {
            _colorAdjustments.contrast.value = v;
        }
    }

    private static void CreateVolume()
    {
        GameObject go = new GameObject("GraphicOptionsVolume");
        Object.DontDestroyOnLoad(go);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _colorAdjustments = profile.Add<ColorAdjustments>();
        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;
        _colorAdjustments.postExposure.value = Brightness;
        _colorAdjustments.contrast.value = Contrast;
        volume.profile = profile;
    }


    private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Game)
        {
            return;
        }

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data == null || data.renderType != CameraRenderType.Base)
        {
            return;
        }

        data.renderPostProcessing = true;
        data.antialiasing = ToCameraMode(AntiAliasing);
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        SaveOriginals();

        _antiAliasing = null;

        ApplySsao(Ssao);
        ApplyMsaa(AntiAliasing);
        ApplyShadows(ShadowQuality);
        CreateVolume();

        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;

#if UNITY_EDITOR
        Application.quitting -= RestoreAsset;
        Application.quitting += RestoreAsset;
#endif
    }

    private static void SaveOriginals()
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset == null)
        {
            _savedOriginals = false;
            return;
        }

        ScreenSpaceAmbientOcclusion ssao = FindSsao();
        _originalSsao = ssao != null && ssao.isActive;
        _originalMsaa = asset.msaaSampleCount;
        _originalShadowDistance = asset.shadowDistance;
        _originalShadowResolution = asset.mainLightShadowmapResolution;
        _originalShadowCascades = asset.shadowCascadeCount;
        _savedOriginals = true;
    }

#if UNITY_EDITOR
    private static void RestoreAsset()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        if (!_savedOriginals)
        {
            return;
        }

        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset != null)
        {
            asset.msaaSampleCount = _originalMsaa;
            asset.shadowDistance = _originalShadowDistance;
            asset.mainLightShadowmapResolution = _originalShadowResolution;
            asset.shadowCascadeCount = _originalShadowCascades;
        }

        ApplySsao(_originalSsao);
    }
#endif
}
