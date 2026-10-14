using TMPro;
using UnityEngine;

public class DayPhaseHUD : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _displayText;

    private TimeManager _time;

    private void OnEnable()
    {
        _time = TimeManager.Instance;
        if (_time == null)
        {
            return;
        }

        _time.OnTextTimeChange += HandleTextUpdate;
        HandleTextUpdate();
    }

    private void OnDisable()
    {
        if (_time != null)
        {
            _time.OnTextTimeChange -= HandleTextUpdate;
        }
        _time = null;
    }

    private void HandleTextUpdate()
    {
        if (_displayText == null)
        {
            return;
        }

        int day = Mathf.Max(1, _time.DayCount);
        string phase = _time.CurrentTimePhase == TimeOfDay.Day ? "낮" : "밤";
        _displayText.text = $"{day}일차 - {phase} {_time.GetTextTime()}";
    }
}
