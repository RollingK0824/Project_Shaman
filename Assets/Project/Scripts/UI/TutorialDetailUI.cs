using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialDetailUI : MonoBehaviour
{
    [Header("튜토리얼 상세 패널")]
    [SerializeField] private string _name;
    [SerializeField] private List<GameObject> _tutorialDetailPanels = new List<GameObject>();
    [SerializeField] private TMP_Text _pageNumText;

    public string Name => _name;
    public List<GameObject> DetailPanels => _tutorialDetailPanels;

    private int _currentNumber = 0;

    public void OnEnable()
    {
        _currentNumber = 0;
        _pageNumText.text = $"{_currentNumber + 1} / {_tutorialDetailPanels.Count}";

        OpenPanel(_currentNumber);
    }

    public void TryClickNextButton()
    {
        if (_currentNumber >= _tutorialDetailPanels.Count - 1) return;

        _currentNumber++;
        PrintPage();

        OpenPanel(_currentNumber);
    }

    public void TryClickPrevButton()
    {
        if (_currentNumber <= 0) return;

        _currentNumber--;
        PrintPage();

        OpenPanel(_currentNumber);
    }

    private void OpenPanel(int idx)
    {
        foreach (GameObject panel in _tutorialDetailPanels)
        {
            panel.SetActive(false);
        }

        _tutorialDetailPanels[idx].SetActive(true);
    }

    public void PrintPage()
    {
        _pageNumText.text = $"{_currentNumber + 1} / {_tutorialDetailPanels.Count}";
    }
}

