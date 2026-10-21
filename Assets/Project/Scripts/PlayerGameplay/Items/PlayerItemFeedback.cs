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
    private Transform _motion;
    private ItemData _data;
    private float _started = -10f, _messageUntil;
    private string _message;
    public bool IsPlaying => _motion != null && Time.time >= _started && Time.time - _started < .6f;
    private void Awake() { _items=GetComponent<PlayerItemController>(); _input=GetComponent<PlayerInputReader>(); }
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
        if (visual == null || item == null || !(item.UseAction is BellUseAction || item.UseAction is GongUseAction)) return;
        foreach (var source in visual.GetComponentsInChildren<AudioSource>(true))
        { source.Stop(); source.mute = true; Destroy(source); }
    }
    private static bool Supported(ItemData item) => item != null &&
        (item.UseAction is BellUseAction || item.UseAction is SaltUseAction ||
         item.UseAction is BeanMugwortUseAction || item.UseAction is GongUseAction || item.UseAction is BarrierTalismanUseAction);
    public void PlayRemoteAudio(ItemData item)
    {
        if (!Supported(item) || !PlayerActionGuard.CanAct(gameObject)) return;
        var clip=item.UseAction is BellUseAction ? _bell : item.UseAction is SaltUseAction ? _salt :
            item.UseAction is BeanMugwortUseAction ? _throw : item.UseAction is GongUseAction ? _gong : null;
        GetComponent<PlayerAudioController>()?.PlayRemoteItem(clip, _volume);
    }
    public void Play(ItemData item)
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject) || !Supported(item)) return;
        _started=Time.time; _message="사용 중"; _messageUntil=Time.time+.45f;
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
        bool visible=local && Supported(item) && (_input == null || !_input.GameplayInputBlocked);
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
    private void OnDisable()
    {
        if (_motion != null) { _motion.localPosition=Vector3.zero; _motion.localRotation=Quaternion.identity; }
        if (_statusRoot != null) _statusRoot.SetActive(false);
        else if (_status != null) _status.gameObject.SetActive(false);
    }
}
