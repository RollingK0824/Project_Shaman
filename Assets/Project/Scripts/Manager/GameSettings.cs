using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GameSettings
{
    // Mouse sensitivity
    private const string SENSITIVITY_KEY = "MouseSensitivity";
    private const float MIN_SENSITIVITY = 0.2f;
    private const float MAX_SENSITIVITY = 3f;
    private static float? _mouseSensitivity;

    public static float MouseSensitivity
    {
        get
        {
            if (_mouseSensitivity == null)
            {
                _mouseSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 1f);
            }
            return _mouseSensitivity.Value;
        }
    }

    public static void SetMouseSensitivity(float v)
    {
        v = Mathf.Clamp(v, MIN_SENSITIVITY, MAX_SENSITIVITY);
        _mouseSensitivity = v;
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, v);
    }



    // Screen Resolution & Fullscreen
    public static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

    public static void SetFullscreen(bool on)
    {
        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    public static List<Vector2Int> GetResolutions()
    {
        List<Vector2Int> result = new List<Vector2Int>();

        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!result.Contains(size))
            {
                result.Add(size);
            }
        }

        return result;
    }

    public static void SetResolution(Vector2Int r)
    {
        Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
    }



    // VSync & FrameRate
    private const string VSYNC_KEY = "VSync",
                         FRAME_CAP_KEY = "FrameCap";

    public static readonly int[] FrameCapOptions = { 30, 60, 120, 144, -1 };
    public static bool VSync => PlayerPrefs.GetInt(VSYNC_KEY, 0) == 1;
    public static int FrameCap => PlayerPrefs.GetInt(FRAME_CAP_KEY, 60);

    public static void SetVSync(bool on)
    {
        PlayerPrefs.SetInt(VSYNC_KEY, on ? 1 : 0);
        ApplyFrameRate();
    }

    public static void SetFrameCap(int fps)
    {
        PlayerPrefs.SetInt(FRAME_CAP_KEY, fps);
        ApplyFrameRate();
    }


    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameCap;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        ApplyFrameRate();
    }
}
