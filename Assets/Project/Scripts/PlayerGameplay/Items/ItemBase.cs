using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    public ItemData Data { get; private set; }
    public GameObject Owner { get; private set; }

    public virtual void Initialize(
        ItemData data,
        GameObject owner)
    {
        Data = data;
        Owner = owner;
    }

    public abstract void OnUseStarted();

    public virtual void OnUseCanceled()
    {
    }
}