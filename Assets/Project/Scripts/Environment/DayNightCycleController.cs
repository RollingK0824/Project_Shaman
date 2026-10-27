using UnityEngine;

[System.Serializable]
public class MoonPhase
{
    public string name = "Full Moon";
    [Tooltip("이 달 위상의 밤 스카이박스 큐브맵 (_NightTex)")]
    public Cubemap nightCubemap;
    [Tooltip("달 밝기 (0 = 삭, 1 = 보름). 밤 조명·환경광·안개 밝기에 반영")]
    [Range(0f, 1f)]
    public float moonBrightness = 1f;
    [Tooltip("이 위상의 밤 스카이박스 노출(_Exposure) 배율. 1 = 원본, 낮을수록 어둡다")]
    [Range(0f, 2f)]
    public float skyboxExposure = 1f;
}

[ExecuteAlways]
public class DayNightCycleController : MonoBehaviour
{
    private const string CustomSkyboxShaderName = "Custom/DayNightCubemapSkybox";
#if UNITY_EDITOR
    private const string DefaultSkyboxMaterialPath = "Assets/Project/Visuals/Materials/Environment/Skybox/DayNightSkybox.mat";
#endif

    private static readonly int RotationProp = Shader.PropertyToID("_Rotation");
    private static readonly int PitchProp = Shader.PropertyToID("_Pitch");
    private static readonly int NightRotationProp = Shader.PropertyToID("_NightRotation");
    private static readonly int NightPitchProp = Shader.PropertyToID("_NightPitch");
    private static readonly int BlendProp = Shader.PropertyToID("_Blend");
    private static readonly int ExposureProp = Shader.PropertyToID("_Exposure");
    private static readonly int NightTexProp = Shader.PropertyToID("_NightTex");

    [Header("References")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Material skyboxMaterial;

    [Header("Cycle Control (0~0.5 = Day, 0.5~0.6 = Dusk, 0.6~0.9 = Night, 0.9~1.0 = Dawn)")]
    [Range(0f, 1f)]
    [SerializeField] private float cycleProgress = 0.15f;
    [SerializeField] private bool autoSimulatePreview = true;
    [SerializeField] private float previewCycleDuration = 24f;

    [Header("AllSkyFree Skybox & Shadow Trajectory")]
    [Tooltip("화면에 보이는 동안 해/달이 좌측(동쪽)에서 우측(서쪽)으로 이동하는 수평 각도 범위")]
    [SerializeField] private float startAzimuthAngle = -65f;
    [SerializeField] private float endAzimuthAngle = 65f;
    [Tooltip("스카이박스 해/달의 상하 고도 변화 폭 (너무 크면 지평선이 올라오므로 자연스러운 범위로 제한)")]
    [Range(0f, 15f)]
    [SerializeField] private float skyboxVerticalArcDegrees = 8f;
    [Tooltip("Directional Light의 최소/최대 고도 각도 (오브젝트 그림자 길이 변화)")]
    [SerializeField] private float minLightElevation = 10f;
    [SerializeField] private float maxLightElevation = 48f;

    [Header("Day / Night Visual Settings")]
    [SerializeField] private Color dayLightColor = new Color(0.95f, 0.91f, 0.85f, 1f);
    [SerializeField] private Color sunsetLightColor = new Color(0.95f, 0.58f, 0.38f, 1f);
    [SerializeField] private Color nightLightColor = new Color(0.22f, 0.35f, 0.52f, 1f);
    [SerializeField] private float dayLightIntensity = 1.1f;
    [SerializeField] private float nightLightIntensity = 0.6f;
    [SerializeField] private Color dayAmbientColor = new Color(0.24f, 0.29f, 0.40f, 1f);
    [SerializeField] private Color nightAmbientColor = new Color(0.08f, 0.12f, 0.20f, 1f);
    [SerializeField] private Color dayFogColor = new Color(0.51f, 0.54f, 0.60f, 1f);
    [SerializeField] private Color nightFogColor = new Color(0.06f, 0.09f, 0.14f, 1f);

    [Header("Moon Phases (하루마다 다음 위상으로 넘어감)")]
    [SerializeField] private MoonPhase[] moonPhases;
    [Tooltip("1일차의 달 위상 인덱스")]
    [SerializeField] private int startMoonPhaseIndex;
    [Tooltip("TimeManager가 없을 때(테스트 씬·에디터) 사용할 위상 인덱스. 미리보기 사이클이 한 바퀴 돌 때마다 증가")]
    [SerializeField] private int previewMoonPhaseIndex;

    [Header("Environment Reflections (Lighting > Environment > Intensity Multiplier)")]
    [Tooltip("낮의 반사 강도. Gameplay 씬의 기존 값 0.244")]
    [Range(0f, 1f)]
    [SerializeField] private float dayReflectionIntensity = 0.244f;
    [Tooltip("밤, 삭(달 밝기 0)일 때의 반사 강도")]
    [Range(0f, 1f)]
    [SerializeField] private float newMoonReflectionIntensity = 0.1f;
    [Tooltip("밤, 보름(달 밝기 1)일 때의 반사 강도")]
    [Range(0f, 1f)]
    [SerializeField] private float fullMoonReflectionIntensity = 0.244f;

    private float _lastCycleProgress = -1f;
    private Texture _originalNightTex;
    private bool _nightTexOverridden;
    private Quaternion _lastLightRotation = Quaternion.identity;
    private double _lastEditorTime;

    private void OnEnable()
    {
        EnsureValidReferences();

        _lastCycleProgress = cycleProgress;

        // 에디터(비재생)에서는 씬에 저장된 라이트·환경 값을 덮어쓰지 않는다. 씬을 열 때마다 씬이 변경 상태가 된다.
        if (Application.isPlaying)
        {
            ApplyFromCycleProgress(cycleProgress);
        }

#if UNITY_EDITOR
        // 해당 주석 해제시 Scene에서도 play 중인 환경처럼 테스트 가능
        //_lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
        //UnityEditor.EditorApplication.update -= OnEditorUpdate;
        //UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
    }

    private void OnDisable()
    {
        // 머티리얼 에셋(.mat)에 위상 큐브맵이 저장되지 않도록 원래 밤 텍스처로 되돌린다.
        if (_nightTexOverridden && skyboxMaterial != null)
        {
            skyboxMaterial.SetTexture(NightTexProp, _originalNightTex);
            _nightTexOverridden = false;
        }

#if UNITY_EDITOR
        //UnityEditor.EditorApplication.update -= OnEditorUpdate;
#endif
    }

    public void EnsureValidReferences()
    {
        if (directionalLight == null)
        {
            directionalLight = GetComponent<Light>();
            if (directionalLight == null)
            {
                directionalLight = RenderSettings.sun;
            }
        }

#if UNITY_EDITOR
        if (skyboxMaterial == null || skyboxMaterial.shader == null || skyboxMaterial.shader.name != CustomSkyboxShaderName)
        {
            Material loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultSkyboxMaterialPath);
            if (loaded != null)
            {
                skyboxMaterial = loaded;
            }
        }
#endif

        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }

        if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
    }

#if UNITY_EDITOR
    //private void OnEditorUpdate()
    //{
    //    if (Application.isPlaying || !autoSimulatePreview)
    //    {
    //        _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
    //        return;
    //    }

    //    double now = UnityEditor.EditorApplication.timeSinceStartup;
    //    float dt = (float)(now - _lastEditorTime);
    //    if (dt < 0.033f)
    //    {
    //        return;
    //    }

    //    _lastEditorTime = now;

    //    if (previewCycleDuration > 0.1f)
    //    {
    //        cycleProgress = Mathf.Repeat(cycleProgress + dt / previewCycleDuration, 1f);
    //        _lastCycleProgress = cycleProgress;
    //        ApplyFromCycleProgress(cycleProgress);
    //        UnityEditor.SceneView.RepaintAll();
    //        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    //    }
    //}
#endif

    private void OnValidate()
    {
        EnsureValidReferences();

        // 씬 로드·도메인 리로드 직후 첫 호출은 기록만 한다. cycleProgress를 직접 바꿨을 때만 반영한다.
        if (_lastCycleProgress < 0f)
        {
            _lastCycleProgress = cycleProgress;
            return;
        }

        if (!Mathf.Approximately(cycleProgress, _lastCycleProgress))
        {
            _lastCycleProgress = cycleProgress;
            ApplyFromCycleProgress(cycleProgress);
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            if (TimeManager.Instance != null && TimeManager.Instance.IsRunning)
            {
                float intoCycle = (float)(TimeManager.Instance.Elapsed % TimeManager.CYCLE_DURATION);
                cycleProgress = Mathf.Clamp01(intoCycle / TimeManager.CYCLE_DURATION);
                _lastCycleProgress = cycleProgress;
                ApplyFromCycleProgress(cycleProgress);
                return;
            }

            if (autoSimulatePreview && previewCycleDuration > 0.1f)
            {
                float next = cycleProgress + Time.deltaTime / previewCycleDuration;
                if (next >= 1f)
                {
                    previewMoonPhaseIndex++;
                }
                cycleProgress = Mathf.Repeat(next, 1f);
                _lastCycleProgress = cycleProgress;
                ApplyFromCycleProgress(cycleProgress);
                return;
            }
        }

        if (!autoSimulatePreview && directionalLight != null && directionalLight.transform.rotation != _lastLightRotation)
        {
            SyncSkyboxToManualLightDirection();
        }
    }

    public void ApplyFromCycleProgress(float t)
    {
        EnsureValidReferences();
        if (directionalLight == null)
        {
            return;
        }

        t = Mathf.Repeat(t, 1f);

        // Timeline layout (matching TimeManager's ~57% Day / ~43% Night ratio):
        // - Dawn crossfade : [0.90 .. 1.00] (Night fades out 1->0 on Right, Day fades in on Left)
        // - Pure Day       : [0.00 .. 0.50] (Blend = 0, Sun sweeps Left -> Center -> Right)
        // - Dusk crossfade : [0.50 .. 0.60] (Day fades out 0->1 on Right, Night fades in on Left)
        // - Pure Night     : [0.60 .. 0.90] (Blend = 1, Moon sweeps Left -> Center -> Right)
        const float duskStart = 0.50f;
        const float duskEnd = 0.60f;
        const float dawnStart = 0.90f;

        float nightBlend;
        float twilightDip = 1f;

        if (t < duskStart)
        {
            nightBlend = 0f;
        }
        else if (t < duskEnd)
        {
            float u = (t - duskStart) / (duskEnd - duskStart);
            nightBlend = Mathf.SmoothStep(0f, 1f, u);
            twilightDip = 1f - Mathf.Sin(u * Mathf.PI) * 0.25f;
        }
        else if (t < dawnStart)
        {
            nightBlend = 1f;
        }
        else
        {
            float u = (t - dawnStart) / (1f - dawnStart);
            nightBlend = Mathf.SmoothStep(1f, 0f, u);
            twilightDip = 1f - Mathf.Sin(u * Mathf.PI) * 0.25f;
        }

        // 1. Continuous Day (Cold Sunset) trajectory across [0.90 -> 1.00 -> 0.00 -> 0.60] (total duration = 0.70)
        //    During [0.60 .. 0.90], nightBlend is 1.0 (Day skybox is 100% invisible), so it resets unseen!
        float sunElapsed = Mathf.Repeat(t - dawnStart, 1f);
        float sunProgress = Mathf.Clamp01(sunElapsed / 0.70f);
        float sunAzimuth = Mathf.Lerp(startAzimuthAngle, endAzimuthAngle, sunProgress);
        float sunArc = Mathf.Sin(sunProgress * Mathf.PI);
        float sunPitch = Mathf.Lerp(-skyboxVerticalArcDegrees * 0.5f, skyboxVerticalArcDegrees, sunArc);

        // 2. Continuous Night (Cold Night) trajectory across [0.50 -> 0.60 -> 0.90 -> 1.00] (total duration = 0.50)
        //    During [0.00 .. 0.50], nightBlend is 0.0 (Night skybox is 100% invisible), so it resets unseen!
        float moonElapsed = Mathf.Repeat(t - duskStart, 1f);
        float moonProgress = Mathf.Clamp01(moonElapsed / 0.50f);
        float moonAzimuth = Mathf.Lerp(startAzimuthAngle, endAzimuthAngle, moonProgress);
        float moonArc = Mathf.Sin(moonProgress * Mathf.PI);
        float moonPitch = Mathf.Lerp(-skyboxVerticalArcDegrees * 0.5f, skyboxVerticalArcDegrees, moonArc);

        MoonPhase moonPhase = GetCurrentMoonPhase();
        float moonBrightness = moonPhase != null ? moonPhase.moonBrightness : 1f;

        // 3. Apply rotations and blend to AllSkyFree Skybox Material
        Material mat = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
        if (mat != null)
        {
            if (RenderSettings.skybox != mat)
            {
                RenderSettings.skybox = mat;
            }

            if (mat.HasProperty(RotationProp)) mat.SetFloat(RotationProp, NormalizeAngle360(sunAzimuth));
            if (mat.HasProperty(PitchProp)) mat.SetFloat(PitchProp, sunPitch);
            if (mat.HasProperty(NightRotationProp)) mat.SetFloat(NightRotationProp, NormalizeAngle360(moonAzimuth));
            if (mat.HasProperty(NightPitchProp)) mat.SetFloat(NightPitchProp, moonPitch);
            if (mat.HasProperty(BlendProp)) mat.SetFloat(BlendProp, nightBlend);
            float phaseExposure = moonPhase != null ? moonPhase.skyboxExposure : 1f;
            if (mat.HasProperty(ExposureProp)) mat.SetFloat(ExposureProp, twilightDip * Mathf.Lerp(1f, phaseExposure, nightBlend));

            // 위상은 하루(DayCount)가 바뀌는 t = 0에 바뀐다. 이때 nightBlend = 0이라 교체가 보이지 않는다.
            if (moonPhase != null && moonPhase.nightCubemap != null && mat.HasProperty(NightTexProp)
                && mat.GetTexture(NightTexProp) != moonPhase.nightCubemap)
            {
                if (!_nightTexOverridden)
                {
                    _originalNightTex = mat.GetTexture(NightTexProp);
                    _nightTexOverridden = true;
                }
                mat.SetTexture(NightTexProp, moonPhase.nightCubemap);
            }
        }

        // 4. Directional Light & Shadows:
        //    Handover occurs at Dusk midpoint (t = 0.55) and Dawn midpoint (t = 0.95),
        //    where directionalLight.intensity smoothly reaches 0 so shadows never snap!
        const float duskMid = 0.55f;
        const float dawnMid = 0.95f;
        bool isSunLightActive = (t >= dawnMid || t < duskMid);

        if (isSunLightActive)
        {
            float lightElapsed = Mathf.Repeat(t - dawnMid, 1f);
            float lightProgress = Mathf.Clamp01(lightElapsed / 0.60f);
            float lightArc = Mathf.Sin(lightProgress * Mathf.PI);
            float lightElevation = Mathf.Lerp(minLightElevation, maxLightElevation, lightArc);

            Vector3 sunDir = Quaternion.Euler(-lightElevation, sunAzimuth, 0f) * Vector3.forward;
            directionalLight.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
            directionalLight.color = Color.Lerp(sunsetLightColor, dayLightColor, Mathf.Clamp01(lightArc * 1.3f));
            directionalLight.intensity = dayLightIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lightArc / 0.2f));
        }
        else
        {
            float lightElapsed = t - duskMid;
            float lightProgress = Mathf.Clamp01(lightElapsed / 0.40f);
            float lightArc = Mathf.Sin(lightProgress * Mathf.PI);
            float lightElevation = Mathf.Lerp(minLightElevation, maxLightElevation, lightArc);

            Vector3 moonDir = Quaternion.Euler(-lightElevation, moonAzimuth, 0f) * Vector3.forward;
            directionalLight.transform.rotation = Quaternion.LookRotation(-moonDir, Vector3.up);
            directionalLight.color = nightLightColor;
            directionalLight.intensity = nightLightIntensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lightArc / 0.2f));
        }

        _lastLightRotation = directionalLight.transform.rotation;

        // 5. Ambient & Fog colors
        RenderSettings.ambientSkyColor = Color.Lerp(dayAmbientColor, nightAmbientColor, nightBlend);
        RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbientColor, nightAmbientColor, nightBlend);
        RenderSettings.ambientGroundColor = Color.Lerp(dayAmbientColor * 0.8f, nightAmbientColor * 0.8f, nightBlend);
        RenderSettings.fogColor = Color.Lerp(dayFogColor, nightFogColor, nightBlend);

        // 6. Environment Reflections: 밤에는 달이 밝을수록 반사 강도가 올라간다.
        float nightReflection = Mathf.Lerp(newMoonReflectionIntensity, fullMoonReflectionIntensity, moonBrightness);
        RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflectionIntensity, nightReflection, nightBlend);
    }

    public int CurrentMoonPhaseIndex
    {
        get
        {
            if (moonPhases == null || moonPhases.Length == 0)
            {
                return -1;
            }

            int dayIndex = previewMoonPhaseIndex;
            if (Application.isPlaying && TimeManager.Instance != null && TimeManager.Instance.IsRunning)
            {
                dayIndex = Mathf.Max(0, TimeManager.Instance.DayCount - 1);
            }

            int index = (startMoonPhaseIndex + dayIndex) % moonPhases.Length;
            return index < 0 ? index + moonPhases.Length : index;
        }
    }

    public MoonPhase GetCurrentMoonPhase()
    {
        int index = CurrentMoonPhaseIndex;
        return index < 0 ? null : moonPhases[index];
    }

    public void SyncSkyboxToManualLightDirection()
    {
        if (directionalLight == null)
        {
            return;
        }

        _lastLightRotation = directionalLight.transform.rotation;

        Material mat = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
        if (mat == null)
        {
            return;
        }

        Vector3 skyDir = -directionalLight.transform.forward;
        float azimuthDeg = NormalizeAngle360(Mathf.Atan2(skyDir.x, skyDir.z) * Mathf.Rad2Deg);
        float altitudeDeg = Mathf.Asin(Mathf.Clamp(skyDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float pitchDeg = Mathf.Clamp((altitudeDeg - 16.5f) * 0.4f, -15f, 15f);

        if (mat.HasProperty(RotationProp)) mat.SetFloat(RotationProp, azimuthDeg);
        if (mat.HasProperty(PitchProp)) mat.SetFloat(PitchProp, pitchDeg);
        if (mat.HasProperty(NightRotationProp)) mat.SetFloat(NightRotationProp, azimuthDeg);
        if (mat.HasProperty(NightPitchProp)) mat.SetFloat(NightPitchProp, pitchDeg);
    }

    private static float NormalizeAngle360(float deg)
    {
        deg %= 360f;
        if (deg < 0f)
        {
            deg += 360f;
        }
        return deg;
    }
}
