using UnityEngine;

public class TestBellReactable : MonoBehaviour, IBellReactable
{
    public void ReactToBell(GameObject user)
    {
        Debug.Log(
            $"[BellReaction] {name} / 방울에 반응 / 사용자 = {user.name}",
            gameObject
        );
    }
}