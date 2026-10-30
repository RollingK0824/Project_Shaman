using UnityEngine;
using UnityEngine.Rendering.Universal;

// No colliders, damage, inventory writes or successful-use reports.
public sealed class ItemVfxPresentation : MonoBehaviour
{
    private static int _live;
    private float _expires;
    private bool _counted;
    public static GameObject Spawn(GameObject prefab, Vector3 point, Quaternion rotation, float seconds, Transform parent=null)
    {
        if(prefab==null || Application.isBatchMode || _live>=32 || !float.IsFinite(point.sqrMagnitude)) return null;
        var go=Instantiate(prefab,point,rotation,parent);
        var life=go.AddComponent<ItemVfxPresentation>();life._expires=Time.time+Mathf.Clamp(seconds,.1f,8f);life._counted=true;_live++;
        foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
        return go;
    }
    public static bool Pulse(GameObject owner,bool gong)
    {
        if(!gong)return Chime(owner);
        var lib=ItemVfxLibrary.Default;if(!owner||!lib)return false;
        var pos=owner.transform.position+Vector3.up*1.25f+owner.transform.forward*.85f;
        var view=owner.GetComponent<PlayerViewModeController>();
        var camera=owner.GetComponentInChildren<Camera>();
        bool firstPerson=PlayerLocalPresentation.IsLocal(owner) && camera!=null &&
            (view==null || view.CurrentViewMode==PlayerViewModeController.ViewMode.FirstPerson);
        if(firstPerson)pos=camera.transform.position+camera.transform.rotation*GongViewPulse.CameraOffset;
        var go=Spawn(gong?lib.GongPulse:lib.BellPulse,pos,Quaternion.identity,1.8f);
        if(!go)return false;
        if(firstPerson)go.AddComponent<GongViewPulse>().Bind(owner,camera,view);
        // Per-camera, temporary opaque copies. Do not change the project's URP asset or team cameras on disk.
        foreach(var renderCamera in Camera.allCameras)
        {
            if(renderCamera.cameraType!=CameraType.Game || !renderCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType!=CameraRenderType.Base)continue;
            var lease=renderCamera.GetComponent<ItemVfxOpaqueLease>()??renderCamera.gameObject.AddComponent<ItemVfxOpaqueLease>();lease.Request(data,1.9f);
        }
        return true;
    }
    public static bool Chime(GameObject owner, Transform heldSource=null)
    {
        var lib=ItemVfxLibrary.Default;if(!owner||!lib)return false;
        var point=owner.transform.position+Vector3.up*1.25f+owner.transform.forward*.35f;
        var view=owner.GetComponent<PlayerViewModeController>();
        bool firstPerson=heldSource!=null && PlayerLocalPresentation.IsLocal(owner) &&
            view!=null && view.CurrentViewMode==PlayerViewModeController.ViewMode.FirstPerson;
        if(firstPerson)
        {
            if(!TryGetBellEmissionPoint(heldSource,out point))point=heldSource.position;
        }
        else if(owner.TryGetComponent<PlayerItemController>(out var items))
        {
            if(items.TryGetThirdPersonBellEffectPoint(out var bellPoint))point=bellPoint;
            else if(items.TryGetThirdPersonReleasePoint(out var hand))point=hand;
        }
        return Spawn(lib.BellPulse,point,Quaternion.identity,1.4f)!=null;
    }
    // The Bell_* mesh parts are the metal bells; the visual root is near the handle.
    // World bounds account for first-person sizing, third-person grip and bell swing.
    public static bool TryGetBellEmissionPoint(Transform visual, out Vector3 point)
    {
        point = visual != null ? visual.position : Vector3.zero;
        if (visual == null) return false;
        bool found = false;
        Bounds bells = default;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                !renderer.gameObject.activeInHierarchy ||
                !renderer.name.StartsWith("Bell_", System.StringComparison.Ordinal)) continue;
            if (!found) { bells = renderer.bounds; found = true; }
            else bells.Encapsulate(renderer.bounds);
        }
        if (found) point = bells.center;
        return found;
    }

    public static void Barrier(Vector3 point) {var l=ItemVfxLibrary.Default;if(l)Spawn(l.BarrierActivate,point,Quaternion.identity,2f);}
    public static void Talisman(GameObject owner) {var l=ItemVfxLibrary.Default;if(l&&owner)Spawn(l.TalismanActivate,owner.transform.position+Vector3.up*.15f,Quaternion.identity,2f,owner.transform);}
    public static void Trail(Transform projectile) {var l=ItemVfxLibrary.Default;if(l&&projectile)Spawn(l.BeanTrail,projectile.position,Quaternion.identity,6.5f,projectile);}
    public static void Impact(Vector3 point) {var l=ItemVfxLibrary.Default;if(l)Spawn(l.BeanImpact,point,Quaternion.identity,2f);}
    private void Update(){if(Time.time>=_expires)Destroy(gameObject);}
    private void OnDestroy(){if(_counted){_live=Mathf.Max(0,_live-1);_counted=false;}}
}
