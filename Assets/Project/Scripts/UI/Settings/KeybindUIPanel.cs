using TMPro;
using UnityEngine;

// 키바인딩 패널. 수첩 설정 탭의 "키 설정" 버튼 OnClick → Open(), 닫기 버튼 → Close(), "기본값으로" 버튼 → ResetAll()
public class KeybindUIPanel : UIPanel
{
    // Optional: the panel this one was opened from (NotebookPanel). When it closes (N, death), this closes too
    [SerializeField]
    private UIPanel _owner;

    // Optional: the shared "이미 사용 중인 키입니다" text that the rows also reference
    [SerializeField]
    private TMP_Text _messageText;

    private InputManager _input;

    private void OnEnable()
    {
        _input = InputManager.Instance;

        if (_messageText != null)
        {
            _messageText.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // Cached: InputManager.Instance would create a new object while the game is quitting
        // A rebind left running would keep waiting for a key with the UI map off
        if (_input != null && _input.IsRebinding)
        {
            _input.CancelRebind();
        }
    }

    private void LateUpdate()
    {
        if (_owner == null)
        {
            return;
        }

        // Checks whether the owner is visible, not whether UIManager opened it (test scenes activate the notebook directly)
        if (!_owner.gameObject.activeInHierarchy && UIManager.Instance.IsOpen(this))
        {
            Close();
        }
    }

    // "기본값으로" button
    public void ResetAll()
    {
        if (_input == null || _input.IsRebinding)
        {
            return;
        }

        _input.ResetAll();
    }
}
