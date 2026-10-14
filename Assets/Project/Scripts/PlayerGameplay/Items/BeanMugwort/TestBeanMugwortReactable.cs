using UnityEngine;

public class TestBeanMugwortReactable
    : MonoBehaviour, IBeanMugwortReactable
{
    public bool ReactToBeanMugwort(
        GameObject user,
        float slowMultiplier,
        float duration)
    {
        Debug.Log(
            $"[BeanMugwort Test] 적중 성공! " +
            $"속도 배율={slowMultiplier}, " +
            $"지속시간={duration}초",
            this
        );

        return true;
    }
}