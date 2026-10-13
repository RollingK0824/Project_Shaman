using UnityEngine;
using UnityEngine.InputSystem;

public class NotebookToggle : MonoBehaviour
{
    [SerializeField]
    private NotebookPanel _notebook;

    void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (UIManager.Instance.IsOpen(_notebook))
            {
                UIManager.Instance.Close(_notebook);
            }
            else
            {
                UIManager.Instance.Open(_notebook);
            }
        }
    }
}
