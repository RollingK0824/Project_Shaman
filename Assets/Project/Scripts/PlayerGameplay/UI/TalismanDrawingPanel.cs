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
    // InputManager의 공용 액션 (Open에서 캐시, 종료 중 Instance를 다시 부르지 않도록)
    private ShamanInput _actions;
    public bool IsOpen { get; private set; }
    public bool IsConfirmingDiscard => _discardDialog != null && _discardDialog.activeSelf;
    public TalismanDrawingData CurrentDrawing => _draft;
    private void Awake()
    {
        BuildBrushControls();
        _clearButton.onClick.AddListener(ClearDrawing);
        _closeButton.onClick.AddListener(RequestClose);
        if (_nextButton != null) _nextButton.gameObject.SetActive(false);
        _nameInput.characterLimit = TalismanDrawingData.MaxNameLength;
        _nameInput.onValueChanged.AddListener(_ => RefreshSaveButton());
        if (_effectDropdown != null) _effectDropdown.onValueChanged.AddListener(_ => RefreshSaveButton());
    }
    // Keep tools outside the paper; dialogs stay above them and block input as before.
    private void BuildBrushControls()
    {
        var parent = _clearButton.transform.parent;
        var style = Instantiate(_clearButton, parent);
        style.name = "BrushTexture";
        var rect = (RectTransform)style.transform;
        rect.anchoredPosition = new Vector2(270, 180);
        rect.sizeDelta = new Vector2(190, 44);
        var label = style.GetComponentInChildren<TMP_Text>();
        label.rectTransform.sizeDelta = rect.sizeDelta;
        string[] names = { "질감: 기본", "질감: 먹붓", "질감: 거친 붓" };
        label.text = names[_drawingCanvas.BrushStyle];
        style.onClick = new Button.ButtonClickedEvent();
        style.onClick.AddListener(() =>
        {
            _drawingCanvas.EndDrawing();
            _drawingCanvas.BrushStyle = (_drawingCanvas.BrushStyle + 1) % names.Length;
            label.text = names[_drawingCanvas.BrushStyle];
        });
        CreateSizeSlider(parent, "펜 크기", 85, _drawingCanvas.PenRadius, 8,
            value => { _drawingCanvas.EndDrawing(); _drawingCanvas.PenRadius = value; });
        CreateSizeSlider(parent, "지우개 크기", -15, _drawingCanvas.EraserRadius, 16,
            value => { _drawingCanvas.EndDrawing(); _drawingCanvas.EraserRadius = value; });
        style.transform.SetSiblingIndex(_clearButton.transform.GetSiblingIndex() + 1);
    }
    private void CreateSizeSlider(Transform parent, string title, float y, int initial, int maximum, System.Action<int> changed)
    {
        var label = Instantiate(_clearButton.GetComponentInChildren<TMP_Text>(), parent);
        label.name = title + " Label";
        label.raycastTarget = false;
        var lr = label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(.5f, .5f);
        lr.anchoredPosition = new Vector2(270, y + 30);
        lr.sizeDelta = new Vector2(200, 36);
        label.text = title + ": " + initial;
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(270, y);
        rect.sizeDelta = new Vector2(180, 24);
        go.GetComponent<Image>().color = new Color(.25f, .20f, .16f, .95f);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(go.transform, false);
        var hr = (RectTransform)handle.transform;
        hr.sizeDelta = new Vector2(18, 6);
        handle.GetComponent<Image>().color = new Color(.88f, .72f, .43f);
        var slider = go.GetComponent<Slider>();
        slider.handleRect = hr;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 1; slider.maxValue = maximum; slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(initial);
        slider.onValueChanged.AddListener(value => { changed((int)value); label.text = title + ": " + (int)value; });
        label.transform.SetSiblingIndex(_clearButton.transform.GetSiblingIndex() + 1);
        go.transform.SetSiblingIndex(_clearButton.transform.GetSiblingIndex() + 1);
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
        _actions = InputManager.Instance.Actions;
        _actions.UI.Cancel.performed += OnCancelAction; _actions.Drawing.Undo.performed += OnUndoAction;
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
    private void OnDestroy() => Close();
    private void OnCancelAction(InputAction.CallbackContext context)
    {
        if (!IsOpen || GameMenuUI.IsOpen || (_input != null && _input.IsCancelReservedForUI)) return;
        _input?.MarkCancelHandled();
        RequestClose();
    }
    private void OnUndoAction(InputAction.CallbackContext context) => Undo();
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
        if (_actions != null) { _actions.UI.Cancel.performed -= OnCancelAction; _actions.Drawing.Undo.performed -= OnUndoAction; }
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
