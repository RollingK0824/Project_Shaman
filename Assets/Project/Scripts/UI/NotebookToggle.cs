using UnityEngine;

public class NotebookToggle : MonoBehaviour
{
    [SerializeField]
    private NotebookPanel _notebook;

    void Update()
    {
        // A bound gameplay notebook owns input; retain this scene-only preview toggle otherwise.
        var gameplay = _notebook != null ? _notebook.GetComponentInParent<PlayerNotebookUI>() : null;
        if (gameplay != null && gameplay.IsBound) return;
        if (InputManager.Instance.Actions.UI.Notebook.WasPressedThisFrame())
        {
            if (UIManager.Instance.IsOpen(_notebook))
            {
                UIManager.Instance.Close(_notebook);
            }
            else if (!UIManager.Instance.IsMenuOpen)
            {
                UIManager.Instance.Open(_notebook);
            }
        }
    }
}
