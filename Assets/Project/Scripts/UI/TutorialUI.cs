using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [Header("튜토리얼 패널")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private List<TutorialDetailUI> _detailPanels = new List<TutorialDetailUI>();

    private TutorialDetailUI _detailUI;

    private int _lastIndex = 0;

    private void OnEnable()
    {
        OpenPanel(_lastIndex);
    }

    public void OnClickButton(int index)
    {
        OpenPanel(index);
    }

    public void OnClickNextButton()
    {
        _detailUI.TryClickNextButton();
    }

    public void OnClickPrevButton()
    {
        _detailUI.TryClickPrevButton();
    }

    private void OpenPanel(int idx)
    {
        if (idx < 0 || idx >= _detailPanels.Count) return;

        foreach (TutorialDetailUI panel in _detailPanels)
        {
            panel.gameObject.SetActive(false);
        }

        _lastIndex = idx;
        _detailUI = _detailPanels[idx];
        _detailUI.gameObject.SetActive(true);
        _nameText.text = _detailUI.Name;
    }
}