using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TalismanDrawingPanel : MonoBehaviour
{
    [SerializeField] private TalismanDrawingCanvas _drawingCanvas;
    [SerializeField] private TalismanDrawingSurface _surface;
    [SerializeField] private TMP_Text _instanceLabel;
    [SerializeField] private Button _clearButton, _closeButton, _nextButton;
    [SerializeField] private GameObject _discardDialog, _saveDialog;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private TMP_Dropdown _effectDropdown;
    [SerializeField] private Button _saveButton;
    [SerializeField] private TMP_Text _saveStatus;
    private readonly System.Collections.Generic.List<TalismanEffectDefinition> _choices = new();
    private bool _hasInk;
    public string SelectedEffectId => _effectDropdown != null && _effectDropdown.value > 0 && _effectDropdown.value <= _choices.Count
        ? _choices[_effectDropdown.value - 1].EffectId : null;
    private PlayerTalismanCollection _collection;
    private TalismanItemData _item;
    private PlayerInputReader _input;
    private PlayerHealth _health;
    private TalismanDrawingData _draft;
    private bool _previousInputBlock, _cursorVisible;
    private CursorLockMode _cursorLock;
    private InputAction _closeAction, _undoAction;
    public bool IsOpen { get; private set; }
    public bool IsConfirmingDiscard => _discardDialog != null && _discardDialog.activeSelf;
    public TalismanDrawingData CurrentDrawing => _draft;
    private void Awake()
    {
        _closeAction = new InputAction("CloseTalismanDrawing", InputActionType.Button, "<Keyboard>/escape");
        _closeAction.performed += _ => RequestClose();
        _undoAction = new InputAction("UndoTalismanDrawing", InputActionType.Button);
        _undoAction.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Keyboard>/z");
        _undoAction.performed += _ => Undo();
        _clearButton.onClick.AddListener(ClearDrawing);
        _closeButton.onClick.AddListener(RequestClose);
        if (_nextButton != null) _nextButton.gameObject.SetActive(false);
        _nameInput.characterLimit = TalismanDrawingData.MaxNameLength;
        _nameInput.onValueChanged.AddListener(_ => RefreshSaveButton());
        if (_effectDropdown != null) _effectDropdown.onValueChanged.AddListener(_ => RefreshSaveButton());
    }
    public bool Open(GameObject owner, PlayerTalismanCollection collection, TalismanItemData item)
    {
        if (IsOpen || !PlayerActionGuard.CanAct(owner) || collection == null || item == null || !item.AllowDrawing) return false;
        _input = owner.GetComponent<PlayerInputReader>();
        if (_input == null || !_input.isActiveAndEnabled || _input.GameplayInputBlocked) return false;
        var selected = collection.CreateDraft(item);
        if (selected == null) return false;
        _collection = collection; _item = item; _health = owner.GetComponent<PlayerHealth>();
        _draft = selected.CreateDraft();
        _choices.Clear();
        var labels = new System.Collections.Generic.List<string> { "효과 선택" };
        foreach (var effect in item.Effects) if (effect != null && effect.IsValid) { _choices.Add(effect); labels.Add(effect.DisplayName); }
        if (_effectDropdown != null) { _effectDropdown.ClearOptions(); _effectDropdown.AddOptions(labels); _effectDropdown.SetValueWithoutNotify(0); _effectDropdown.RefreshShownValue(); }
        _hasInk = false;
        if (_saveStatus != null) _saveStatus.text = "그림, 이름(1~12자), 효과를 지정해 주세요.";
        _previousInputBlock = _input.GameplayInputBlocked;
        _cursorVisible = Cursor.visible; _cursorLock = Cursor.lockState;
        IsOpen = true; gameObject.SetActive(true);
        _input.enabled = false; _input.SetGameplayInputBlocked(true);
        _closeAction.Enable(); _undoAction.Enable();
        if (_health != null) _health.Died += OnDied;
        _collection.SelectionChanged += ValidateInstance;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _drawingCanvas.Bind(_draft); _surface.SetPaper(item.PaperTexture); _surface.Bind(_draft);
        _instanceLabel.text = string.IsNullOrEmpty(selected.CustomName) ? item.DisplayName : selected.CustomName;
        _discardDialog.SetActive(false); _saveDialog.SetActive(false); _drawingCanvas.enabled = true; SetPen();
        return true;
    }
    private void ValidateInstance()
    {
        if (_collection.GetComponent<PlayerInventory>().Contains(_item) && _collection.Find(_draft.Id, out _) == null) return;
        Close();
    }
    private void OnDestroy() { Close(); _closeAction?.Dispose(); _undoAction?.Dispose(); }
    private void OnDied(GameObject source) => Close();
    private void OnDisable() { if (IsOpen) Close(); }
    public void RequestClose()
    {
        if (!IsOpen) return;
        _drawingCanvas.EndDrawing(); _drawingCanvas.enabled = false;
        _saveDialog.SetActive(false); _discardDialog.SetActive(true);
    }
    public void ContinueDrawing() { if (!IsOpen) return; _discardDialog.SetActive(false); _saveDialog.SetActive(false); _drawingCanvas.enabled = true; }
    public void RequestSave()
    {
        if (!IsOpen) return;
        _drawingCanvas.EndDrawing(); _drawingCanvas.enabled = false;
        _discardDialog.SetActive(false); _saveDialog.SetActive(true);
        _hasInk = _draft.IsValidDrawing();
        _nameInput.text = string.IsNullOrEmpty(_draft.CustomName) ? _item.DisplayName : _draft.CustomName;
        RefreshSaveButton();
        _nameInput.ActivateInputField();
    }
    public void SaveNamed() => Save(_nameInput.text);
    public bool Save(string customName)
    {
        if (!IsOpen || !TalismanDrawingData.ValidName(customName) || string.IsNullOrEmpty(SelectedEffectId)) return false;
        if (!_collection.RequestSave(_item, _draft, customName, SelectedEffectId))
        {
            if (_saveStatus != null) _saveStatus.text = _collection.CanExecute ? "저장하지 못했습니다. 그림과 보유 수량을 확인해 주세요." : "서버 저장 확인을 기다리고 있습니다.";
            return false;
        }
        Close(); return true;
    }
    private void RefreshSaveButton()
    {
        if (_saveButton != null) _saveButton.interactable = _hasInk && TalismanDrawingData.ValidName(_nameInput.text) && !string.IsNullOrEmpty(SelectedEffectId);
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; _drawingCanvas.EndDrawing(); _draft = null;
        _closeAction?.Disable(); _undoAction?.Disable();
        if (_collection != null) _collection.SelectionChanged -= ValidateInstance;
        if (_health != null) _health.Died -= OnDied;
        if (_input != null) { _input.enabled = true; _input.SetGameplayInputBlocked(_previousInputBlock); }
        Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
        gameObject.SetActive(false);
    }
    private bool CanDraw => IsOpen && !_discardDialog.activeSelf && !_saveDialog.activeSelf;
    public void SetPen() { _drawingCanvas.EndDrawing(); _drawingCanvas.Eraser = false; }
    public void SetEraser() { _drawingCanvas.EndDrawing(); _drawingCanvas.Eraser = true; }
    public void ClearDrawing() { if (CanDraw) { _drawingCanvas.EndDrawing(); _draft.Clear(); } }
    public void Undo() { if (CanDraw) { _drawingCanvas.EndDrawing(); _draft.Undo(); } }
    // Kept for old callers; a draft always belongs to one physical sheet.
    public void NextSheet() { }
}
