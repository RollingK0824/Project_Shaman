using TMPro;
using UnityEngine;

public class DayPhaseHUD : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _displayText;

    public void Start()
    {
        if (TimeManager.Instance)
        {
            TimeManager.Instance.OnTextTimeChange += HandleTextUpdate;
        }
    }

    public void HandleTextUpdate()
    {
        _displayText.text = TimeManager.Instance.GetTextTime();
    }
}
