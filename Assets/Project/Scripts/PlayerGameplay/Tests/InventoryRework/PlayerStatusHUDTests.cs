#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PlayerStatusHUDTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> objects = new();
    PlayerInventory inventory; PlayerHealth health; PlayerStressController stress;
    PlayerTalismanEffectController effects; PlayerStatusHUD hud; PlayerStressHUD stressHUD;
    T Keep<T>(T o) where T:Object { objects.Add(o);return o; }
    static T Data<T>(string name) where T:Object => AssetDatabase.LoadAssetAtPath<T>(
        "Assets/Project/Data/Items/" + (name.StartsWith("Talismans/") ? name : "Items/" + name));
    static void Set(object o,string name,object value) => o.GetType().GetField(name,Private).SetValue(o,value);
    static T Get<T>(object o,string name) => (T)o.GetType().GetField(name,Private).GetValue(o);
    static void Call(object o,string name,params object[] args) => o.GetType().GetMethod(name,Private).Invoke(o,args);
    TMP_Text[] Feed => Get<RectTransform>(hud,"_feedRoot").GetComponentsInChildren<TMP_Text>().Where(x=>x.name=="Item Count").ToArray();
    string Timer => Get<TMP_Text>(hud,"_timerText").text;
    [SetUp] public void Setup()
    {
        var player=Keep(new GameObject("Status HUD QA player"));player.transform.position=new Vector3(900,0,900);
        health=player.AddComponent<PlayerHealth>();stress=player.AddComponent<PlayerStressController>();stress.enabled=false;
        inventory=player.AddComponent<PlayerInventory>();effects=player.AddComponent<PlayerTalismanEffectController>();
        Keep(inventory.SharedMaterials.gameObject);
        stressHUD=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/UI/StressHUD.prefab"))).GetComponent<PlayerStressHUD>();
        stressHUD.Bind(stress,health);hud=stressHUD.StatusHUD;
    }
    [TearDown] public void Cleanup()
    {
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);objects.Clear();
    }
    [Test] public void PersonalAndSharedGainsMergeWithoutRemovalOrFailedUseAlerts()
    {
        var salt=Data<ItemData>("SaltItemData.asset");var coarse=Data<ItemData>("CoarseSaltMaterial.asset");
        Assert.IsTrue(inventory.TryAddItem(salt,2));Assert.IsTrue(inventory.SharedMaterials.TryAdd(inventory.gameObject,coarse,2));
        Assert.AreEqual(2,Feed.Length);Assert.IsTrue(Feed.Any(x=>x.text==salt.DisplayName+" +2"));Assert.IsTrue(Feed.Any(x=>x.text==coarse.DisplayName+" +2"));
        Assert.IsTrue(inventory.RemoveItem(salt));Assert.IsTrue(inventory.SharedMaterials.TryRemove(inventory.gameObject,coarse));
        Assert.AreEqual(2,Feed.Length);Assert.IsTrue(Feed.All(x=>x.text.EndsWith("+2")));
        inventory.SharedMaterials.SetAuthorityCheck(()=>false);Assert.IsFalse(inventory.TryAddItem(coarse));Assert.AreEqual(2,Feed.Length);
        var badges=Get<RectTransform>(hud,"_feedRoot").GetComponentsInChildren<TMP_Text>().Where(x=>x.name=="Inventory Type").Select(x=>x.text);
        CollectionAssert.AreEquivalent(new[]{"개인","공용"},badges);
    }
    [Test] public void RebindDoesNotReplayExistingItemsOrDuplicateSubscriptions()
    {
        var salt=Data<ItemData>("SaltItemData.asset");var coarse=Data<ItemData>("CoarseSaltMaterial.asset");
        inventory.TryAddItem(salt);inventory.TryAddItem(coarse);stressHUD.Bind(stress,health);Assert.AreEqual(0,Feed.Length);
        inventory.TryAddItem(coarse);inventory.TryAddItem(salt);Assert.AreEqual(2,Feed.Length);Assert.IsTrue(Feed.All(x=>x.text.EndsWith("+1")));
        var replacement=Keep(new GameObject("Replacement shared")).AddComponent<SharedMaterialInventory>();replacement.TryAdd(inventory.gameObject,coarse,50);
        inventory.BindSharedMaterials(replacement);Assert.AreEqual(2,Feed.Length);replacement.TryAdd(inventory.gameObject,coarse);Assert.IsTrue(Feed.Any(x=>x.text==coarse.DisplayName+" +2"));
    }
    [UnityTest] public IEnumerator FeedFadesAndExpiresWithoutScaledTimeAndDisableUnsubscribes()
    {
        Set(hud,"_feedDuration",.3f);Set(hud,"_fadeDuration",.2f);inventory.TryAddItem(Data<ItemData>("SaltItemData.asset"));
        float old=Time.timeScale;
        try {Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);Assert.Less(Get<RectTransform>(hud,"_feedRoot").GetComponentInChildren<CanvasGroup>().alpha,1);yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(0,Feed.Length);}
        finally {Time.timeScale=old;}
        hud.enabled=false;inventory.TryAddItem(Data<ItemData>("SaltItemData.asset"));Assert.AreEqual(0,Feed.Length);
        hud.enabled=true;inventory.TryAddItem(Data<ItemData>("SaltItemData.asset"));Assert.AreEqual(1,Feed.Length);StringAssert.EndsWith("+1",Feed[0].text);
    }
    [Test] public void FeedCapacityIsBoundedAndDoesNotCreateAnotherCanvas()
    {
        foreach(var file in new[]{"SaltItemData.asset","BellItemData.asset","GongItemData.asset","MirrorItemData.asset","CoinItemData.asset"})inventory.TryAddItem(Data<ItemData>(file));
        Assert.AreEqual(4,Feed.Length);Assert.AreEqual(1,stressHUD.GetComponentsInChildren<Canvas>(true).Length);
        Assert.IsTrue(stressHUD.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget));
    }
    [UnityTest] public IEnumerator TimedEffectsCountDownRefreshExpireAndInstantEffectsHaveNoTimer()
    {
        var swift=Keep(Object.Instantiate(Data<TalismanEffectDefinition>("Talismans/Effects/move_speed.asset")));Set(swift,"_duration",2.8f);
        Assert.IsTrue(effects.TryApply(swift));Call(hud,"LateUpdate");StringAssert.Contains("[신속]   0:03",Timer);
        stress.SetStress(50);Assert.IsTrue(effects.TryApply(Data<TalismanEffectDefinition>("Talismans/Effects/stress_gain.asset")));health.ReceiveDamage(50,null);
        Assert.IsTrue(effects.TryApply(Data<TalismanEffectDefinition>("Talismans/Effects/stress_recovery.asset")));Call(hud,"LateUpdate");Assert.IsFalse(Timer.Contains("청심"));Assert.IsFalse(Timer.Contains("회생"));
        yield return new WaitForSeconds(1.1f);Call(hud,"LateUpdate");StringAssert.Contains("0:02",Timer);
        Assert.IsTrue(effects.TryApply(swift));Call(hud,"LateUpdate");StringAssert.Contains("0:03",Timer);
        effects.ResetEffects();Call(hud,"LateUpdate");Assert.IsFalse(Get<RectTransform>(hud,"_timerRoot").gameObject.activeSelf);
        Set(swift,"_duration",.1f);effects.TryApply(swift);yield return new WaitForSeconds(.15f);Call(hud,"LateUpdate");Assert.AreEqual("",Timer);
    }
    BarrierWard CreateWard(float duration)
    {
        var ward=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Items/Talismans/BarrierWard.prefab"))).GetComponent<BarrierWard>();
        Set(ward,"_maxLifetime",duration);Call(ward,"Prepare",inventory.gameObject,null);Call(ward,"Arm");return ward;
    }
    [UnityTest] public IEnumerator ActualWardTimerEndsWithWardAndDeathClearsAllHud()
    {
        var ward=CreateWard(2.8f);Call(hud,"LateUpdate");StringAssert.Contains("결계 부적   0:03",Timer);Assert.IsTrue(BarrierWard.IsProtecting(inventory.gameObject));
        yield return new WaitForSeconds(1.1f);Call(hud,"LateUpdate");StringAssert.Contains("0:02",Timer);
        ward.Expire();Call(hud,"LateUpdate");Assert.AreEqual("",Timer);
        Assert.IsTrue(hud.ApplyBarrierPresentation(1,20));inventory.TryAddItem(Data<ItemData>("SaltItemData.asset"));health.ReceiveDamage(100,null);Call(hud,"LateUpdate");
        Assert.AreEqual(0,Feed.Length);Assert.IsFalse(Get<RectTransform>(hud,"_timerRoot").gameObject.activeSelf);Assert.IsFalse(stressHUD.GetComponent<Canvas>().enabled);
    }
    [Test] public void BarrierPresentationRejectsStaleDataAndNeverGrantsProtection()
    {
        Assert.IsTrue(hud.ApplyBarrierPresentation(2,30));Assert.IsFalse(BarrierWard.IsProtecting(inventory.gameObject));StringAssert.Contains("0:30",Timer);
        Assert.IsFalse(hud.ApplyBarrierPresentation(1,60));Assert.IsFalse(hud.ApplyBarrierPresentation(3,float.NaN));Assert.IsFalse(hud.ApplyBarrierPresentation(3,-1));
        Assert.IsTrue(hud.ApplyBarrierPresentation(3,0));Assert.AreEqual("",Timer);
    }
    [UnityTest] public IEnumerator RenderStatusHudAndGreyBarrier()
    {
        var camera=Keep(new GameObject("Status preview camera")).AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.22f,.25f);
        camera.transform.position=inventory.transform.position+new Vector3(0,1.2f,-5);camera.transform.LookAt(inventory.transform.position+Vector3.up);
        var ward=CreateWard(60);var visual=ward.GetComponentInChildren<BarrierWardVisual>();Assert.IsTrue(visual.IsShowing);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Visuals/Materials/Items/Talisman/BarrierShell.mat");var color=material.GetColor("_BaseColor");Assert.Less(Mathf.Abs(color.r-color.b),.06f);Assert.Less(color.maxColorComponent,.7f);Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
        effects.TryApply(Data<TalismanEffectDefinition>("Talismans/Effects/move_speed.asset"));
        inventory.TryAddItem(Data<ItemData>("LampOilMaterial.asset"));inventory.TryAddItem(Data<ItemData>("CoarseSaltMaterial.asset"),2);inventory.TryAddItem(Data<ItemData>("Talismans/BarrierTalisman.asset"));
        stress.SetStress(50);yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
        Assert.AreEqual(3,Feed.Length);StringAssert.Contains("결계 부적",Timer);StringAssert.Contains("[신속]",Timer);
        var layout=new InventoryLayoutTests();Set(hud,"_feedDuration",20f);
        try
        {
            foreach(int width in new[]{1280,1600,1920})
            {
                Call(layout,"Size",width,width*9/16);yield return null;yield return null;yield return new WaitForEndOfFrame();
                Assert.AreEqual(width,Screen.width);
                var texture=ScreenCapture.CaptureScreenshotAsTexture();Directory.CreateDirectory("Temp/StatusHUDQA");File.WriteAllBytes($"Temp/StatusHUDQA/HUD-and-Barrier-{width}.png",texture.EncodeToPNG());Object.Destroy(texture);
                var root=Get<RectTransform>(hud,"_timerRoot");var corners=new Vector3[4];root.GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x,0);Assert.Greater(corners[0].y,100*Mathf.Min(Screen.width/1280f,Screen.height/720f));
                Assert.Less(corners[2].x,Screen.width);Assert.Less(corners[2].y,Screen.height);
                var feed=Get<RectTransform>(hud,"_feedRoot");feed.GetWorldCorners(corners);Assert.GreaterOrEqual(corners[0].x,0);Assert.Greater(corners[0].y,200*width/1280f);Assert.Less(corners[2].x,Screen.width);
                foreach(var t in hud.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Assert.IsFalse(t.isTextOverflowing,t.name);}
            }
        }
        finally {layout.Clean();}
    }
}
#endif
