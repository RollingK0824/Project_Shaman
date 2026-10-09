using UnityEngine;

public class TestItemReactable :
    MonoBehaviour,
    ICoinReactable,
    ISaltReactable
{
    [Header("Test Reaction")]
    [SerializeField]
    private bool _acceptCoin = true;

    [SerializeField]
    private bool _reactToSalt = true;

    public bool ReactToCoin(
        GameObject user)
    {
        Debug.Log(
            $"[TestNPC] 엽전 반응 / " +
            $"User: {user.name}",
            gameObject
        );

        return _acceptCoin;
    }

    public bool ReactToSalt(
        GameObject user)
    {
        Debug.Log(
            $"[TestNPC] 소금 반응 / " +
            $"User: {user.name}",
            gameObject
        );

        return _reactToSalt;
    }
}