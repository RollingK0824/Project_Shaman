using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 설정의 마이크 선택 드롭다운. 첫 항목은 "시스템 기본값"
// 열 때마다 장치 목록을 다시 읽음 (헤드셋을 꽂거나 뺀 경우)
public class VoiceDeviceSettingsUI : MonoBehaviour
{
    private const string DEFAULT_OPTION = "시스템 기본값";

    [SerializeField]
    private TMP_Dropdown _inputDropdown;

    private List<string> _inputDevices = new List<string>();

    private void Awake()
    {
        if (_inputDropdown == null)
        {
            return;
        }

        _inputDropdown.onValueChanged.AddListener(HandleInput);
    }

    // 저장된 마이크가 지금 없으면 기본값을 보여 줌 (저장값은 그대로 둠, 다시 꽂으면 복구)
    private void OnEnable()
    {
        if (_inputDropdown == null)
        {
            return;
        }

        _inputDevices = new List<string>(Microphone.devices);

        List<string> options = new List<string> { DEFAULT_OPTION };
        options.AddRange(_inputDevices);

        _inputDropdown.ClearOptions();
        _inputDropdown.AddOptions(options);
        _inputDropdown.SetValueWithoutNotify(_inputDevices.IndexOf(VoiceSettings.InputDevice) + 1);
    }

    private void HandleInput(int index)
    {
        VoiceSettings.SetInputDevice(index == 0 ? "" : _inputDevices[index - 1]);
    }
}
