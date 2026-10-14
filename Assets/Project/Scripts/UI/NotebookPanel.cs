using UnityEngine;
using UnityEngine.UI;

public class NotebookPanel : UIPanel
{
    [SerializeField]
    private GameObject[] _tabs;

    [SerializeField]
    private Button[] _tabButtons;

    private int _currentTab;

    private void Awake()
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i;
            _tabButtons[i].onClick.AddListener(() => SelectTab(index));
        }
    }

    private void OnEnable()
    {
        SelectTab(_currentTab);
    }

    public void SelectTab(int index)
    {
        if (_tabs.Length == 0)
        {
            return;
        }

        _currentTab = Mathf.Clamp(index, 0, _tabs.Length - 1);

        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].SetActive(i == _currentTab);
        }

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            _tabButtons[i].interactable = i != _currentTab;
        }
    }
}
