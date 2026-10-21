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
        bool fullscreen = GameSettings.IsFullscreen;
        _fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        List<string> options = new List<string>();
        foreach (Vector2Int r in GameSettings.GetResolutions())
        {
            options.Add($"{r.x} x {r.y}");
        }

        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);

        // Borderless fullscreen always uses the monitor's size, so the dropdown only works in windowed mode
        SelectResolution(fullscreen ? GameSettings.NativeResolution : new Vector2Int(Screen.width, Screen.height));
        _resolutionDropdown.interactable = !fullscreen;

        RefreshFrameRate();
    }

    // Selects the matching entry, or the largest if the size isn't in the list
    private void SelectResolution(Vector2Int size)
    {
        List<Vector2Int> resolutions = GameSettings.GetResolutions();
        int index = resolutions.IndexOf(size);
        if (index == -1)
        {
            index = resolutions.Count - 1;
        }

        _resolutionDropdown.SetValueWithoutNotify(index);
    }

    private void HandleFullscreen(bool on)
    {
        Vector2Int size = GameSettings.SetFullscreen(on);
        SelectResolution(size);
        _resolutionDropdown.interactable = !on;
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
