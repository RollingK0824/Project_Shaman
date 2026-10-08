using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VoteRowUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _voteCountText;

    [SerializeField]
    private Button _voteButton;

    [SerializeField]
    private GameObject _myVoteMarker;

    int _npcId;
}
