using UnityEngine;
using UnityEngine.InputSystem;

public class NotebookToggle : MonoBehaviour
{
    [SerializeField]
    private NotebookPanel _notebook;

    void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame)
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
