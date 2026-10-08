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

    // 내가 투표한 NPC에만 표시되는 X
    [SerializeField]
    private GameObject _voteMarker;

    private string _npcId;
    private Action<string> _onClick;

    private void Awake()
    {
        _voteButton.onClick.AddListener(HandleClick);
    }

    // 셀을 만들 때 한 번만 호출. 하루 동안 바뀌지 않는 값
    public void Setup(string npcId, string name, Action<string> onClick)
    {
        _npcId = npcId;
        _nameText.SetText(name);
        _onClick = onClick;
    }

    public void Refresh(int votes, bool isMine)
    {
        _voteCountText.SetText(votes.ToString());
        _voteMarker.SetActive(isMine);
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
