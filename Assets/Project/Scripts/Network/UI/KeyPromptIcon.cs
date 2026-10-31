using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 키캡에 현재 바인딩된 키 이름 표시, 키 설정이 바뀌면 다시 그림
public class KeyPromptIcon : MonoBehaviour
{
    [SerializeField] private InputActionReference _action;
    [SerializeField] private TMP_Text _keyText;

    private InputManager _input;

    private void OnEnable()
    {
        _input = InputManager.Instance;
        _input.BindingsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_input != null) _input.BindingsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_action == null || _keyText == null) return;
        // 키 변경은 InputManager 사본에만 적용됨
        InputAction action = _input.Actions.asset.FindAction(_action.action.id);
        if (action == null) return;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isComposite || action.bindings[i].isPartOfComposite) continue;
            _keyText.text = InputManager.GetDisplay(action, i);
            return;
        }
    }
}