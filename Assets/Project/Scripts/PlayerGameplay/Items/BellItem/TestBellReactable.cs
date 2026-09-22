using UnityEngine;

public class TestBellReactable : MonoBehaviour, IBellReactable
{
    [Header("Test Reaction")]
    [SerializeField]
    private string _reactionName =
        "방울에 반응함";

    public void ReactToBell(GameObject user)
    {
        string userName =
            user != null
                ? user.name
                : "Unknown";

        Debug.Log(
            $"[BellReaction] {gameObject.name} / " +
            $"{_reactionName} / " +
            $"사용자 = {userName}",
            gameObject
        );
    }
}