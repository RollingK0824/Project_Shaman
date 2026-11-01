using System;
using Mirror;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Player-owned state and presentation. NPC, time and network systems are read-only dependencies.
[DisallowMultipleComponent]
public sealed class PlayerVillagerTracking : MonoBehaviour
{
    public const float AttachDistance = 2.5f;
    public event Action<string, AI_Core, Vector3, Vector3> AttachRequested;
    public event Action StateChanged;
    public AI_Core Target { get; private set; }
    public double ExpiresAt { get; private set; }
    public float Remaining => TimeManager.Existing == null ? 0 : (float)Math.Max(0, ExpiresAt - TimeManager.Existing.Elapsed);
    private long _version = -1;
    private TimeManager _clock;
    private double _lastTime;
    private GameObject _hud;
    private TMP_Text _label;
    private RectTransform _marker;
    private Camera _camera;
    public bool Active => Target != null && Remaining > 0;
    private bool Online => NetworkClient.active || NetworkServer.active;
    private Camera ViewCamera()
    {
        if (_camera != null && _camera.isActiveAndEnabled) return _camera;
        foreach (var c in GetComponentsInChildren<Camera>(true)) if (c.isActiveAndEnabled) return _camera = c;
        return null;
    }
    private static bool ValidTarget(AI_Core target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.PublicInfo == null || string.IsNullOrEmpty(target.PublicInfo.VillagerId)) return false;
        var registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        return registry == null || !registry.TryGet(target.PublicInfo.VillagerId, out var view) || view.IsAlive;
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    public AI_Core FindTarget(Vector3 origin, Vector3 direction)
    {
        if (!Finite(origin) || !Finite(direction) || direction.sqrMagnitude < .5f) return null;
        var hits = Physics.RaycastAll(origin, direction.normalized, AttachDistance + 2f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            // The first surface blocks attachment: never select a resident through a wall.
            var target = hit.collider.GetComponentInParent<AI_Core>();
            return ValidTarget(target) && Vector3.Distance(transform.position, hit.point) <= AttachDistance ? target : null;
        }
        return null;
    }
    public bool CanAttach(string id, AI_Core target, Vector3 origin, Vector3 direction)
    {
        var items = GetComponent<PlayerItemController>();
        var input = GetComponent<PlayerInputReader>();
        return CanAttachTarget(target, origin, direction) && items != null && items.CanUseItems && items.EquippedTalismanInstanceId == id &&
            GetComponent<PlayerInventory>().GetQuickSlotInstanceId(items.SelectedSlotIndex) == id &&
            (input == null || !input.GameplayInputBlocked) && !(GetComponent<PlayerNotebookUI>() is { IsOpen:true }) &&
            !(GetComponent<PlayerViewModeController>() is { IsBusy:true }) &&
            (UIManager.Existing == null || !UIManager.Existing.IsMenuOpen);
    }
    public bool CanAttachOnServer(string id, AI_Core target, Vector3 origin, Vector3 direction)
    {
        var equipment = GetComponent<NetPlayerEquipment>();
        return NetworkServer.active && equipment != null && equipment.ServerHasEquippedTalisman(id) &&
            CanAttachTarget(target, origin, direction);
    }
    private bool CanAttachTarget(AI_Core target, Vector3 origin, Vector3 direction)
    {
        var clock = TimeManager.Existing;
        return isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) && !(Active && Target == target) && clock != null && clock.IsRunning &&
            double.IsFinite(clock.Elapsed) && clock.Elapsed >= 0 &&
            Finite(origin) && Vector3.Distance(origin, transform.position + Vector3.up * 1.5f) <= 3f &&
            target != null && FindTarget(origin, direction) == target &&
            FindTarget(transform.position + Vector3.up * 1.5f, target.transform.position + Vector3.up - (transform.position + Vector3.up * 1.5f)) == target;
    }
    public bool RequestAttach(string id)
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject)) return false;
        var camera = ViewCamera();
        if (camera == null) return false;
        Vector3 origin = camera.transform.position, direction = camera.transform.forward;
        var target = FindTarget(origin, direction);
        if (!CanAttach(id, target, origin, direction))
        {
            GetComponent<PlayerItemFeedback>()?.Unavailable(Active && Target == target ? "이미 추적 중인 주민입니다" : "가까운 주민을 바라보세요 (시간 시스템 필요)");
            return false;
        }
        if (Online)
        {
            if (AttachRequested == null) GetComponent<PlayerItemFeedback>()?.Unavailable("추적 부적의 네트워크 연결이 필요합니다");
            else AttachRequested.Invoke(id, target, origin, direction);
            return false; // Never consume or show a successful attachment before server confirmation.
        }
        return GetComponent<PlayerTalismanCollection>().TryUseTracking(id, target, origin, direction);
    }
    internal void Commit(AI_Core target)
    {
        _clock = TimeManager.Existing; _lastTime = _clock.Elapsed;
        // One slot per owner: replacing the resident never adds another marker or restarts a duration.
        Target = target; ExpiresAt = EndOfCurrentDay(_lastTime);
    }
    private static double EndOfCurrentDay(double elapsed) =>
        (Math.Floor(elapsed / TimeManager.CYCLE_DURATION) + 1) * TimeManager.CYCLE_DURATION;
    internal void Publish() { try { StateChanged?.Invoke(); } catch(Exception e) { Debug.LogException(e, this); } }
    // Call only from an owner TargetRpc. Server supplies a resolved NPC and absolute game-clock expiry.
    public bool ApplyConfirmed(long version, AI_Core target, double expiresAt)
    {
        if (!NetworkClient.active || NetworkServer.active || !PlayerLocalPresentation.IsLocal(gameObject) ||
            version <= _version || !PlayerActionGuard.CanAct(gameObject)) return false;
        var clock = TimeManager.Existing;
        if (target != null && (!ValidTarget(target) || clock == null || !clock.IsRunning ||
            double.IsNaN(expiresAt) || double.IsInfinity(expiresAt) || expiresAt <= clock.Elapsed ||
            !double.IsFinite(clock.Elapsed) || clock.Elapsed < 0 ||
            Math.Abs(expiresAt - EndOfCurrentDay(clock.Elapsed)) > .01)) return false;
        _version = version; Target = target; ExpiresAt = target == null ? 0 : expiresAt;
        _clock = clock; _lastTime = clock != null ? clock.Elapsed : 0; Publish(); return true;
    }
    public void ResetRoundTracking() { ResetTracking(); _version = -1; }
    public void ResetTracking()
    {
        bool had = !ReferenceEquals(Target, null); Target = null; ExpiresAt = 0;
        if (_hud != null) _hud.SetActive(false);
        if (had) Publish();
    }
    private void OnDisable() => ResetTracking();
    private void OnDestroy() { if (_hud != null) Destroy(_hud); }
    private void LateUpdate()
    {
        if (!ReferenceEquals(Target, null) && (!PlayerActionGuard.CanAct(gameObject) || !ValidTarget(Target) || TimeManager.Existing != _clock ||
            _clock == null || !_clock.IsRunning || _clock.Elapsed < _lastTime || Remaining <= 0)) ResetTracking();
        if (_clock != null) _lastTime = _clock.Elapsed;
        bool show = Active && PlayerLocalPresentation.IsLocal(gameObject) && ViewCamera() != null &&
            !(GetComponent<PlayerNotebookUI>() is { IsOpen:true }) && (UIManager.Existing == null || !UIManager.Existing.IsMenuOpen);
        if (!show) { if (_hud != null) _hud.SetActive(false); return; }
        if (_hud == null) BuildHUD();
        _hud.SetActive(true);
        Vector3 position = Target.transform.position + Vector3.up * 2.1f;
        Vector3 p = _camera.WorldToViewportPoint(position);
        Vector2 v = new Vector2(p.x-.5f,p.y-.5f);
        bool inside = p.z > 0 && p.x > .12f && p.x < .88f && p.y > .16f && p.y < .88f;
        string arrow = "◇";
        if (!inside)
        {
            if (p.z < 0) v = -v;
            if (v.sqrMagnitude < .0001f) v = Vector2.down;
            arrow = Mathf.Abs(v.x)>Mathf.Abs(v.y) ? (v.x>0 ? "→" : "←") : (v.y>0 ? "↑" : "↓");
            float t = Mathf.Min(.36f/Mathf.Max(.0001f,Mathf.Abs(v.x)),.32f/Mathf.Max(.0001f,Mathf.Abs(v.y))); v *= t;
        }
        _marker.anchorMin = _marker.anchorMax = new Vector2(.5f+v.x,.5f+v.y);
        _marker.anchoredPosition = Vector2.zero;
        string name = Target.PublicInfo != null ? Target.PublicInfo.DisplayName : "주민";
        int seconds = Mathf.CeilToInt(Remaining);
        _label.text = $"{arrow} [추적] {name}\n{Vector3.Distance(transform.position,Target.transform.position):0}m · {seconds/60:00}:{seconds%60:00}";
    }
    private void BuildHUD()
    {
        _hud = new GameObject("Local Villager Tracking HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        _hud.transform.SetParent(transform,false);
        var canvas=_hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
        var scaler=_hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        _marker=new GameObject("Tracking Marker",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();_marker.SetParent(_hud.transform,false);_marker.sizeDelta=new Vector2(220,58);
        var bg=_marker.GetComponent<Image>();bg.color=new Color(.06f,.065f,.07f,.78f);bg.raycastTarget=false;
        _label=new GameObject("Name Distance Time",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();_label.transform.SetParent(_marker,false);
        _label.rectTransform.anchorMin=Vector2.zero;_label.rectTransform.anchorMax=Vector2.one;_label.rectTransform.offsetMin=new Vector2(6,4);_label.rectTransform.offsetMax=new Vector2(-6,-4);
        _label.fontSize=18;_label.alignment=TextAlignmentOptions.Center;_label.color=new Color(.94f,.86f,.65f);_label.raycastTarget=false;_label.richText=false;
        foreach(var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if(font.name=="PlayerItemFont") { _label.font=font;break; }
    }
}
