using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeybindRowUI : MonoBehaviour
{
    private const float MESSAGE_DURATION = 2f;

    [SerializeField]
    private InputActionReference _action;

    // Empty for normal actions; up / down / left / right for the Move rows
    [SerializeField]
    private string _compositePart;

    [SerializeField]
    private TMP_Text _label;

    [SerializeField]
    private Button _keyButton;

    [SerializeField]
    private TMP_Text _keyText;

    // Optional
    [SerializeField]
    private Button _resetButton;

    // Optional, can be one text shared by all rows
    [SerializeField]
    private TMP_Text _messageText;

    private InputManager _input;

    // The game runs on InputManager's copy of the actions, not the asset file the reference points at
    private InputAction Action => _input.Actions.asset.FindAction(_action.action.id);

    private int BindingIndex
    {
        get
        {
            if (!string.IsNullOrEmpty(_compositePart))
            {
                return InputManager.FindPart(Action, _compositePart);
            }

            var bindings = Action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    private void Awake()
    {
        if (_keyButton != null)
        {
            _keyButton.onClick.AddListener(HandleKeyClicked);
        }

        if (_resetButton != null)
        {
            _resetButton.onClick.AddListener(HandleResetClicked);
        }
    }

    private void OnEnable()
    {
        _input = InputManager.Instance;
        _input.BindingsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_input != null)
        {
            _input.BindingsChanged -= Refresh;
        }

        StopAllCoroutines();
        if (_messageText != null)
        {
            _messageText.gameObject.SetActive(false);
        }
    }

    private bool IsValid()
    {
        if (_action == null || _action.action == null || Action == null)
        {
            Debug.LogWarning($"[KeybindRowUI] {name}: no action assigned.", this);
            return false;
        }

        if (BindingIndex < 0)
        {
            Debug.LogWarning($"[KeybindRowUI] {name}: binding '{_compositePart}' not found.", this);
            return false;
        }

        return true;
    }

    private void Refresh()
    {
        if (!IsValid())
        {
            return;
        }

        _keyText.text = InputManager.GetDisplay(Action, BindingIndex);
    }

    private void HandleKeyClicked()
    {
        if (!IsValid() || _input.IsRebinding)
        {
            return;
        }

        _keyText.text = "키를 누르세요… (Esc: 취소)";
        _input.StartRebind(Action, BindingIndex, HandleRebindDone);
    }

    private void HandleRebindDone(bool changed)
    {
        if (changed)
        {
            return;
        }

        Refresh();
        if (_input.LastRebindRejected && isActiveAndEnabled)
        {
            StartCoroutine(ShowMessage("이미 사용 중인 키입니다"));
        }
    }

    private void HandleResetClicked()
    {
        if (!IsValid() || _input.IsRebinding)
        {
            return;
        }

        _input.ResetBinding(Action, BindingIndex);
    }

    private IEnumerator ShowMessage(string message)
    {
        if (_messageText == null)
        {
            yield break;
        }

        _messageText.text = message;
        _messageText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(MESSAGE_DURATION);
        _messageText.gameObject.SetActive(false);
    }
}
