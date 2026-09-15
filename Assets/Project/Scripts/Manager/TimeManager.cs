using System.Collections;
using UnityEngine;

public enum TimeOfDay
{
    Morning,
    Noon,
    Night
}

public class TimeManager : SceneSingleton<TimeManager>
{
    public int Day { get; private set; }
    public TimeOfDay CurrentTimeOfDay { get; private set; }

    private int _time = 0;

    public void Start()
    {
        Day = 1;
        CurrentTimeOfDay = TimeOfDay.Morning;

        StartCoroutine(UpdateTime());
    }

    private IEnumerator UpdateTime()
    {
        while (!GameManager.Instance.isGameover)
        {
            ++_time;
            if (_time == 60)
            {
                _time = 0;
                ++Day;

                CurrentTimeOfDay = TimeOfDay.Morning;
            }
            else if (_time == 48)
            {
                CurrentTimeOfDay = TimeOfDay.Night;
            }
            else if (_time == 24)
            {
                CurrentTimeOfDay = TimeOfDay.Noon;
            }

            yield return new WaitForSeconds(1f);
        }
    }
}
