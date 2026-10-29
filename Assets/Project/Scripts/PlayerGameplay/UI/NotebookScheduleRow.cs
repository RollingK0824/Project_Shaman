using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NotebookScheduleRow : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _mark;
    [SerializeField] private GameObject _divider;
    [SerializeField] private TMP_Text[] _cells = new TMP_Text[0];
    // HUD pin (PinManager); optional, rows without one just can't be pinned
    [SerializeField] private Button _pinButton;
    [SerializeField] private Image _pinIcon;

    public Button Button => _button;
    public Image Mark => _mark;
    public GameObject Divider => _divider;
    public TMP_Text[] Cells => _cells;
    public Button PinButton => _pinButton;
    public Image PinIcon => _pinIcon;
}
