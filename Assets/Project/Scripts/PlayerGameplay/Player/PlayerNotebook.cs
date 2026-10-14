using System;
using UnityEngine;

[DefaultExecutionOrder(150)]
public class PlayerNotebook : MonoBehaviour
{
    [SerializeField] private GameObject _presentationPrefab;
    [SerializeField] private Transform _camera;
    [SerializeField] private Vector3 _openPosition = new Vector3(0f, -.14f, .48f);
    [SerializeField] private Vector3 _openEuler = new Vector3(-8f, 0f, 0f);
    [SerializeField, Min(.05f)] private float _transitionDuration = .35f;
    private PlayerInputReader _input;
    private PlayerItemController _items;
    private PlayerViewModeController _view;
    private PlayerHealth _health;
    private PlayerHandSway _sway;
    private GameObject _presentation;
    private Transform _leftLeaf;
    private bool _restoreThird, _cursorVisible;
    private CursorLockMode _cursorLock;
    private float _progress;
    public bool IsOpen { get; private set; }
    public bool IsPresenting => IsOpen || _progress > 0f;
    public Transform UIAnchor { get; private set; }
    public event Action<Transform> Opened;
    public event Action Closed;
    public void Bind(Action<Transform> onOpen, Action onClose) { Opened += onOpen; Closed += onClose; }
    public void Unbind(Action<Transform> onOpen, Action onClose) { Opened -= onOpen; Closed -= onClose; }
    private void Awake()
    {
        _input=GetComponent<PlayerInputReader>(); _items=GetComponent<PlayerItemController>();
        _view=GetComponent<PlayerViewModeController>(); _health=GetComponent<PlayerHealth>(); _sway=GetComponent<PlayerHandSway>();
    }
    private void OnEnable() { _input.NotebookPressed+=Toggle; _input.CancelPressed+=Close; if(_health!=null)_health.Died+=OnDied; }
    private void OnDisable()
    {
        _input.NotebookPressed-=Toggle;_input.CancelPressed-=Close;if(_health!=null)_health.Died-=OnDied;
        CloseImmediate();
    }
    private void OnDied(GameObject source)=>CloseImmediate();
    public void Toggle() { if(IsOpen)Close();else Open(); }
    public bool Open()
    {
        if(IsPresenting||_camera==null||_presentationPrefab==null||!PlayerLocalPresentation.IsLocal(gameObject)||!PlayerActionGuard.CanAct(gameObject)||!_input.isActiveAndEnabled||_input.GameplayInputBlocked||(_view!=null&&_view.IsBusy))return false;
        if(_presentation==null)
        {
            _presentation=Instantiate(_presentationPrefab,_camera,false);
            _leftLeaf=_presentation.transform.Find("LeftLeaf");UIAnchor=_presentation.transform.Find("UIAnchor");
        }
        _restoreThird=_view!=null&&_view.CurrentViewMode==PlayerViewModeController.ViewMode.ThirdPerson;
        if(_restoreThird)_view.ToggleViewMode();
        _cursorVisible=Cursor.visible;_cursorLock=Cursor.lockState;
        _input.SetGameplayInputBlocked(true);
        _items?.SetFirstPersonPresentationSuppressed(true);
        if(_sway!=null)_sway.Suspended=true;
        IsOpen=true;_presentation.SetActive(true);Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        return true;
    }
    public void Close() { if(IsOpen) { IsOpen=false; Closed?.Invoke(); } }
    private void LateUpdate()
    {
        if(!IsPresenting)return;
        if(!PlayerActionGuard.CanAct(gameObject)){CloseImmediate();return;}
        float previous=_progress;
        _progress=Mathf.MoveTowards(_progress,IsOpen?1f:0f,Time.unscaledDeltaTime/_transitionDuration);
        float blend=Mathf.SmoothStep(0f,1f,_progress);
        _presentation.transform.localPosition=_openPosition+Vector3.down*(.45f*(1f-blend));
        _presentation.transform.localRotation=Quaternion.Euler(_openEuler+new Vector3(25f*(1f-blend),0,0));
        if(_leftLeaf!=null)_leftLeaf.localRotation=Quaternion.Euler(0,Mathf.Lerp(-175f,0f,blend),0);
        if(previous<1f&&_progress>=1f&&IsOpen)Opened?.Invoke(UIAnchor);
        if(_progress<=0f)Restore();
    }
    private void CloseImmediate() { bool was=IsOpen;IsOpen=false;_progress=0;if(was)Closed?.Invoke();Restore(); }
    private void Restore()
    {
        if(_presentation==null||!_presentation.activeSelf)return;
        _presentation.SetActive(false);_input.SetGameplayInputBlocked(false);
        _items?.SetFirstPersonPresentationSuppressed(false);if(_sway!=null)_sway.Suspended=false;
        if(_restoreThird&&_view!=null&&PlayerActionGuard.CanAct(gameObject))_view.ToggleViewMode();_restoreThird=false;
        Cursor.visible=_cursorVisible;Cursor.lockState=_cursorLock;
    }
    private void OnDestroy(){if(_presentation!=null)Destroy(_presentation);}
}
