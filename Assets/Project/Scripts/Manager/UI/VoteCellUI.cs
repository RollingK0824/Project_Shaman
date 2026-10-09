using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VoteCellUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _voteCountText;

    [SerializeField]
    private Button _voteButton;

    [SerializeField]
    private Transform _markRoot;

    [SerializeField]
    private Image _markPrefab;

    // X끼리의 가로 간격. X 너비의 절반이면 반쯤 겹쳐 보인다
    [SerializeField]
    private float _markStep = 25f;

    private string _npcId;
    private readonly List<Image> _marks = new List<Image>();

    private Action<string> _onClick;

    private void Awake()
    {
        _voteButton.onClick.AddListener(HandleClick);
    }

    public void Setup(string npcId, string name, Action<string> onClick)
    {
        _npcId = npcId;
        _nameText.SetText(name);
        _onClick = onClick;
    }

    public void Refresh(int votes, IReadOnlyList<Color> markColors)
    {
        _voteCountText.SetText(votes.ToString());

        int needed = markColors != null ? markColors.Count : 0;

        while (_marks.Count < needed)
        {
            Image mark = Instantiate(_markPrefab, _markRoot);
            mark.raycastTarget = false;
            _marks.Add(mark);
        }

        // 가운데를 기준으로 좌우 대칭 배치: 1개면 정중앙, 2개면 양옆, 3개면 가운데 + 양옆
        float firstOffset = -(needed - 1) * 0.5f * _markStep;

        for (int i = 0; i < _marks.Count; i++)
        {
            bool used = i < needed;
            _marks[i].gameObject.SetActive(used);

            if (used)
            {
                _marks[i].color = markColors[i];

                RectTransform rect = _marks[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(firstOffset + i * _markStep, 0f);
            }
        }
    }

    public void SetInteractable(bool interactable)
    {
        _voteButton.interactable = interactable;
    }

    private void HandleClick()
    {
        _onClick?.Invoke(_npcId);
    }
}
