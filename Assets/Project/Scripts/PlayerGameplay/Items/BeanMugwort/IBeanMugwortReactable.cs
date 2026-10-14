using UnityEngine;

public interface IBeanMugwortReactable
{
    bool ReactToBeanMugwort(
        GameObject user,
        float slowMultiplier,
        float duration
    );
}