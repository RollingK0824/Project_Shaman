using UnityEngine;
using System;


// [로컬 클라] 음성 채팅 설정. PlayerPrefs에 저장. 시스템 설정 UI에서 호출
// 다른 클라와 동기화하지 않음 (이 PC에서 어떻게 듣고 보낼지만 결정)
public static class VoiceSettings
{
    private const string VOLUME_KEY = "VoiceVolume", MIC_MUTED_KEY = "VoiceMicMuted";

    private static float? _volume;
    private static bool? _micMuted;

    public static event Action<float> VolumeChanged;
    public static event Action<bool> MicMutedChanged;

    // 0~1. 받은 음성 전체 볼륨 (그 위의 Master 볼륨이 한 번 더 적용)
    public static float Volume
    {
        get
        {
            if (_volume == null) _volume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
            return _volume.Value;
        }
    }

    public static void SetVolume(float v)
    {
        v = Mathf.Clamp01(v);
        _volume = v;
        PlayerPrefs.SetFloat(VOLUME_KEY, v);
        VolumeChanged?.Invoke(v);
    }

    // true면 내 마이크 음성을 보내지 않음 (근거리, 무전 모두)
    public static bool MicMuted
    {
        get
        {
            if (_micMuted == null) _micMuted = PlayerPrefs.GetInt(MIC_MUTED_KEY, 0) == 1;
            return _micMuted.Value;
        }
    }

    public static void SetMicMuted(bool muted)
    {
        if (MicMuted == muted) return;

        _micMuted = muted;
        PlayerPrefs.SetInt(MIC_MUTED_KEY, muted ? 1 : 0);
        MicMutedChanged?.Invoke(muted);
    }

    public static void ToggleMicMuted() => SetMicMuted(!MicMuted);


    private const string INPUT_DEVICE_KEY = "VoiceInputDevice";

    private static string _inputDevice;

    public static event Action<string> InputDeviceChanged;

    // 녹음할 마이크 이름 (Microphone.devices 중 하나). 빈 문자열이면 시스템 기본 마이크
    // 출력 장치는 Unity가 항상 OS 기본 장치로 재생해서 고를 수 없음
    public static string InputDevice
    {
        get
        {
            if (_inputDevice == null) _inputDevice = PlayerPrefs.GetString(INPUT_DEVICE_KEY, "");
            return _inputDevice;
        }
    }

    public static void SetInputDevice(string device)
    {
        device ??= "";
        if (InputDevice == device) return;

        _inputDevice = device;
        PlayerPrefs.SetString(INPUT_DEVICE_KEY, device);
        InputDeviceChanged?.Invoke(device);
    }
}
