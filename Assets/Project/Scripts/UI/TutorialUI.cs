using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [Header("튜토리얼 패널")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private List<GameObject> _tutorialPanels = new List<GameObject>();
    [SerializeField] private List<TutorialDetailUI> _detailPanels = new List<TutorialDetailUI>();

    private TutorialDetailUI _detailUI;

    private int _lastIndex = 0;

    private void Awake()
    {
    }

    public void OnEnable()
    {
        OpenPanel(_lastIndex);
    }

    public void OnClickButton(int index)
    {
        OpenPanel(index);

        _lastIndex = index;
    }

    public void OnClickNextButton()
    {
        _detailPanels[_lastIndex].TryClickNextButton();
    }

    public void OnClickPrevButton()
    {
        _detailPanels[_lastIndex].TryClickPrevButton();
    }

    private void OpenPanel(int idx)
    {
        foreach (GameObject panel in _tutorialPanels)
        {
            panel.SetActive(false);
        }

        _tutorialPanels[idx].SetActive(true);
        _detailUI = _tutorialPanels[idx].GetComponent<TutorialDetailUI>();
        _nameText.text = _detailUI.Name;
    }

    
}
