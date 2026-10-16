using UnityEngine;

public class NotebookItem : ItemBase
{
    public override void OnUseStarted()
    {
        if (Owner != null && PlayerActionGuard.CanAct(Owner)) Owner.GetComponent<PlayerNotebook>()?.RequestOpen();
    }
}
