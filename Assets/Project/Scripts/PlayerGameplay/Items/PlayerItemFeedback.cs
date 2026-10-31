using TMPro;
using UnityEngine;

// Request feedback, not hit confirmation. No damage, inventory or quest-event side effects.
[DisallowMultipleComponent]
public sealed class PlayerItemFeedback : MonoBehaviour
{
    [SerializeField] private TMP_Text _status;
    [SerializeField] private GameObject _statusRoot;
    [SerializeField] private AudioClip _bell, _salt, _throw, _gong;
    [SerializeField, Range(0f,1f)] private float _volume = .4f;
    private PlayerItemController _items;
    private PlayerInputReader _input;
    private QuickSlotUI _quickSlots;
    private float _nextQuickSlotSearch;
    private readonly Vector3[] _hudCorners = new Vector3[4];
    private Transform _motion;
    private ItemData _data;
    private float _started = -10f, _messageUntil;
    private string _message;
    public bool IsPlaying => _motion != null && Time.time >= _started && Time.time - _started < .6f;
    private void Awake() { _items=GetComponent<PlayerItemController>(); _input=GetComponent<PlayerInputReader>(); }
    private PlayerTalismanCollection _talismans;
    private void OnEnable()
    {
        Canvas.willRenderCanvases += PositionUseHint;
        _talismans=GetComponent<PlayerTalismanCollection>();
        if(_talismans!=null)_talismans.UseCommitted+=OnTalismanCommitted;
    }
    private void OnTalismanCommitted(string instanceId)=>ItemVfxPresentation.Talisman(gameObject);
    public void Bind(Transform motion, ItemData item)
    {
        _motion=motion; _data=item; _started=-10f; _messageUntil=0f;
        if (motion != null) PrepareVisual(motion.gameObject, item);
    }
    // BellSwing owns secondary motion, but item use audio has one presentation owner.
    // Remove legacy child speakers on the instance only; source assets are unchanged.
    // BellSwing tolerates a destroyed speaker and keeps its secondary motion running.
    public static void PrepareVisual(GameObject visual, ItemData item)
    {
        if (visual == null || item == null) return;
        if (item.UseAction is SaltUseAction)
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.playOnAwake = false;
                ps.GetComponent<ParticleSystemRenderer>().enabled = false;
            }
        if (!(item.UseAction is BellUseAction || item.UseAction is GongUseAction)) return;
        foreach (var source in visual.GetComponentsInChildren<AudioSource>(true))
        { source.Stop(); source.mute = true; Destroy(source); }
    }
    private static bool Supported(ItemData item) => item != null &&
        (item.UseAction is BellUseAction || item.UseAction is SaltUseAction ||
         item.UseAction is BeanMugwortUseAction || item.UseAction is GongUseAction || item.UseAction is BarrierTalismanUseAction);
    private bool PlayRecorded(ItemData item, bool remote)
    {
        var audio = GetComponent<PlayerAudioController>();
        if (audio == null || !audio.UsesRecordedSounds) return false;
        // Barrier success is audible from its confirmed visual, never from this request.
        if (item.UseAction is BarrierTalismanUseAction) return true;
        var cue = item.UseAction is BellUseAction ? PlayerSoundCue.Bell :
            item.UseAction is SaltUseAction ? PlayerSoundCue.Salt :
            item.UseAction is BeanMugwortUseAction ? PlayerSoundCue.Throw :
            item.UseAction is CoinUseAction ? PlayerSoundCue.Coin : PlayerSoundCue.Gong;
        audio.PlayCue(cue, remote);
        return true;
    }
    public void PlayRemoteAudio(ItemData item)
    {
        if (item == null || (!Supported(item) && !(item.UseAction is CoinUseAction)) || !PlayerActionGuard.CanAct(gameObject)) return;
        if (PlayRecorded(item, true)) return;
        var clip=item.UseAction is BellUseAction ? _bell : item.UseAction is SaltUseAction ? _salt :
            item.UseAction is BeanMugwortUseAction ? _throw : item.UseAction is GongUseAction ? _gong : null;
        GetComponent<PlayerAudioController>()?.PlayRemoteItem(clip, _volume);
    }
    public void Play(ItemData item)
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject) || item == null) return;
        // CoinItem already owns its hand motion; add only its sound here.
        if (item.UseAction is CoinUseAction) { PlayRecorded(item, false); return; }
        if (!Supported(item)) return;
        _started=Time.time; _message="사용 중"; _messageUntil=Time.time+.45f;
        if (PlayRecorded(item, false)) return;
        var clip=item.UseAction is BellUseAction ? _bell : item.UseAction is SaltUseAction ? _salt :
            item.UseAction is BeanMugwortUseAction ? _throw : item.UseAction is GongUseAction ? _gong : null;
        if (clip == null) return;
        GetComponent<PlayerAudioController>()?.PlayLocalItem(clip, _volume);
    }
    public void Unavailable(string message) { _message=message; _messageUntil=Time.time+.8f; }
    private void LateUpdate()
    {
        bool local=PlayerLocalPresentation.IsLocal(gameObject) && PlayerActionGuard.CanAct(gameObject);
        var item=_items != null ? _items.EquippedItemData : null;
        bool lanternMessage=item != null && item.ItemId == "item_lantern" && Time.time<_messageUntil;
        bool visible=local && (Supported(item) || lanternMessage) && (_input == null || !_input.GameplayInputBlocked);
        if (_status != null)
        {
            (_statusRoot != null ? _statusRoot : _status.gameObject).SetActive(visible);
            if (visible) {
                float remaining=_items.RemainingUseCooldown(item);
                _status.text=!_items.CanUseItems ? "지금은 사용할 수 없습니다" :
                    Time.time<_messageUntil ? _message : remaining>.01f ? $"재사용 {remaining:0.0}초" : "좌클릭 · 사용";
                _status.color=!_items.CanUseItems || remaining>.01f ? PlayerItemUIStyle.Muted : PlayerItemUIStyle.Text;
            }
        }
        if (_motion == null) return;
        float t=Time.time-_started;
        float envelope=local && t>=0f && t<.6f ? Mathf.Sin(Mathf.PI*t/.6f) : 0f;
        float angle=_data != null && _data.UseAction is BellUseAction ? Mathf.Sin(t*40f)*10f : -9f;
        _motion.localRotation=Quaternion.Euler(angle*envelope,0,0);
        _motion.localPosition=new Vector3(0,.008f*envelope,.015f*envelope);
    }
    // QuickSlotUI sizes itself in LateUpdate. Align immediately before canvas rendering,
    // converting through screen coordinates because the two HUDs may use different scalers.
    private void PositionUseHint()
    {
        if (_status == null || !PlayerLocalPresentation.IsLocal(gameObject)) return;
        var hint = (_statusRoot != null ? _statusRoot.transform : _status.transform) as RectTransform;
        if (hint == null || !hint.gameObject.activeInHierarchy) return;
        var inventory = GetComponent<PlayerInventory>();
        if (inventory == null) return;
        if (_quickSlots == null || _quickSlots.BoundInventory != inventory || !_quickSlots.isActiveAndEnabled)
        {
            _quickSlots = null;
            if (Time.unscaledTime < _nextQuickSlotSearch) return;
            _nextQuickSlotSearch = Time.unscaledTime + .5f;
            foreach (var candidate in FindObjectsByType<QuickSlotUI>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled && candidate.BoundInventory == inventory)
                { _quickSlots = candidate; break; }
        }
        if (_quickSlots != null) AlignUseHint(hint, _quickSlots.transform as RectTransform);
    }

    private void AlignUseHint(RectTransform hint, RectTransform quick)
    {
        if (quick == null || !(hint.parent is RectTransform parent)) return;
        var quickCanvas = quick.GetComponentInParent<Canvas>();
        var hintCanvas = hint.GetComponentInParent<Canvas>();
        if (quickCanvas == null || hintCanvas == null) return;
        var quickCamera = quickCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : quickCanvas.worldCamera;
        var hintCamera = hintCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hintCanvas.worldCamera;
        quick.GetWorldCorners(_hudCorners);
        float left = float.PositiveInfinity, right = float.NegativeInfinity, top = float.NegativeInfinity;
        foreach (var corner in _hudCorners)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(quickCamera, corner);
            left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x); top = Mathf.Max(top, screen.y);
        }
        hint.GetWorldCorners(_hudCorners);
        var bottom = RectTransformUtility.WorldToScreenPoint(hintCamera, (_hudCorners[0] + _hudCorners[3]) * .5f);
        float gap = 12f * Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        var target = new Vector2((left + right) * .5f, top + gap);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottom, hintCamera, out var currentLocal) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, target, hintCamera, out var targetLocal))
            hint.localPosition += (Vector3)(targetLocal - currentLocal);
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= PositionUseHint;
        if(_talismans!=null)_talismans.UseCommitted-=OnTalismanCommitted;
        if (_motion != null) { _motion.localPosition=Vector3.zero; _motion.localRotation=Quaternion.identity; }
        if (_statusRoot != null) _statusRoot.SetActive(false);
        else if (_status != null) _status.gameObject.SetActive(false);
    }
}
