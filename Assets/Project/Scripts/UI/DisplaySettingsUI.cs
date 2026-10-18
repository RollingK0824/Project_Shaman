using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplaySettingsUI : MonoBehaviour
{
    // screen related stuff
    [SerializeField]
    private Toggle _fullscreenToggle;

    [SerializeField]
    private TMP_Dropdown _resolutionDropdown;

    private void Awake()
    {
        if (_fullscreenToggle != null && _resolutionDropdown != null)
        {
            _fullscreenToggle.onValueChanged.AddListener(HandleFullscreen);
            _resolutionDropdown.onValueChanged.AddListener(HandleResolution);
        }

        if (_vsyncToggle != null && _frameCapDropdown != null)
        {
            _vsyncToggle.onValueChanged.AddListener(HandleVSync);
            _frameCapDropdown.onValueChanged.AddListener(HandleFrameCap);
        }
    }

    private void OnEnable()
    {
        _fullscreenToggle.SetIsOnWithoutNotify(GameSettings.IsFullscreen);

        List<Vector2Int> resolutions = GameSettings.GetResolutions();
        List<string> options = new List<string>();
        int current = resolutions.Count - 1;

        for (int i = 0; i < resolutions.Count; i++)
        {
            Vector2Int r = resolutions[i];
            options.Add($"{r.x} x {r.y}");

            if (r.x == Screen.width && r.y == Screen.height)
            {
                current = i;
            }
        }

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.SetValueWithoutNotify(current);

        RefreshFrameRate();
    }

    private void HandleFullscreen(bool on)
    {
        GameSettings.SetFullscreen(on);
    }

    private void HandleResolution(int index)
    {
        GameSettings.SetResolution(GameSettings.GetResolutions()[index]);
    }


    // vsync and framerate thing
    [SerializeField]
    private Toggle _vsyncToggle;

    [SerializeField]
    private TMP_Dropdown _frameCapDropdown;

    private void RefreshFrameRate()
    {
        if (_vsyncToggle == null || _frameCapDropdown == null)
        {
            return;
        }

        bool vsync = GameSettings.VSync;
        _vsyncToggle.SetIsOnWithoutNotify(vsync);

        int[] caps = GameSettings.FrameCapOptions;
        List<string> options = new List<string>();
        foreach (int fps in caps)
        {
            options.Add(fps == -1 ? "제한 없음" : fps.ToString());
        }

        int current = System.Array.IndexOf(caps, GameSettings.FrameCap);
        if (current == -1)
        {
            current = System.Array.IndexOf(caps, 60);
        }

        _frameCapDropdown.ClearOptions();
        _frameCapDropdown.AddOptions(options);
        _frameCapDropdown.SetValueWithoutNotify(current);
        _frameCapDropdown.interactable = !vsync;
    }

    private void HandleVSync(bool on)
    {
        GameSettings.SetVSync(on);
        _frameCapDropdown.interactable = !on;
    }

    private void HandleFrameCap(int index)
    {
        GameSettings.SetFrameCap(GameSettings.FrameCapOptions[index]);
    }
}
