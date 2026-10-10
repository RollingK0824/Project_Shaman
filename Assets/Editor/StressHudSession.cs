using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class StressHudSession
{
    const string Work="C:/Users/inha/Documents/Codex/2026-10-01/new-chat/work/stress-hud";
    const string Out="C:/Users/inha/Documents/Codex/2026-10-01/new-chat/outputs/StressHUD";
    const string Prefab="Assets/Project/Prefabs/UI/StressHUD.prefab";
    [Serializable] class Request { public string id; public string action; public string[] args; }
    [Serializable] class Result { public string id; public bool ok; public string message; }
    static StressHudSession(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Work+"/request.json"))return;
        var r=JsonUtility.FromJson<Request>(File.ReadAllText(Work+"/request.json"));File.Delete(Work+"/request.json");Directory.CreateDirectory(Out);
        try{Run(r);File.WriteAllText(Work+"/done.json",JsonUtility.ToJson(new Result{id=r.id,ok=true}));}
        catch(Exception e){File.WriteAllText(Work+"/done.json",JsonUtility.ToJson(new Result{id=r.id,ok=false,message=e.ToString()}));}
    }
    static GameObject Player()=>SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.GetComponent<PlayerStressController>());
    static void Run(Request r)
    {
        switch(r.action)
        {
            case "install":Install();break;
            case "play":EditorApplication.isPlaying=true;break;
            case "stop":EditorApplication.isPlaying=false;break;
            case "set":Player().GetComponent<PlayerStressController>().SetStress(float.Parse(r.args[0]));break;
            case "die":Player().GetComponent<PlayerHealth>().SetHealth(0);break;
            case "capture":ScreenCapture.CaptureScreenshot(Out+"/"+r.args[0]+".png");Audit(r.args[0]);break;
            case "audit":Audit("audit");break;
            case "verify-prefabs":VerifyPrefabs();break;
        }
    }
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static Image Image(RectTransform rect,Color color){var i=rect.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
    static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,string value,float size,Vector2 position,Vector2 dimensions,TextAlignmentOptions align,Color color)
    {
        var rect=Rect(name,parent,new Vector2(.5f,1),new Vector2(.5f,1),position,dimensions);
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;
        text.alignment=align;text.color=color;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;
        return text;
    }
    static void Install()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdParty/Font/ChosunCentennial_ttf SDF.asset");
        var root=new GameObject("StressHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
        try
        {
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5;canvas.enabled=false;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            root.GetComponent<CanvasGroup>().blocksRaycasts=false;root.GetComponent<CanvasGroup>().interactable=false;
            var panel=Rect("TopCenterPanel",root.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-22),new Vector2(320,72));panel.pivot=new Vector2(.5f,1);
            Image(panel,new Color(.045f,.065f,.085f,.88f));
            Image(Rect("TopRule",panel,new Vector2(0,1),Vector2.one,new Vector2(0,-1),new Vector2(0,2)),new Color(.65f,.52f,.32f,.85f));
            Text("Title",panel,font,"스트레스",19,new Vector2(-78,-24),new Vector2(128,26),TextAlignmentOptions.MidlineLeft,new Color(.88f,.84f,.74f));
            var value=Text("Value",panel,font,"0.0 / 100",22,new Vector2(80,-24),new Vector2(124,28),TextAlignmentOptions.MidlineRight,new Color(.97f,.94f,.86f));
            var level=Text("Level",panel,font,"안정",13,new Vector2(-117,-48),new Vector2(50,20),TextAlignmentOptions.MidlineLeft,new Color(.57f,.77f,.68f));
            var track=Rect("Gauge",panel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(28,-49),new Vector2(228,6));Image(track,new Color(.19f,.22f,.24f));
            var fill=Image(Rect("Fill",track,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero),new Color(.57f,.77f,.68f));
            var hud=root.AddComponent<PlayerStressHUD>();var so=new SerializedObject(hud);
            so.FindProperty("_canvas").objectReferenceValue=canvas;so.FindProperty("_fill").objectReferenceValue=fill;
            so.FindProperty("_valueText").objectReferenceValue=value;so.FindProperty("_levelText").objectReferenceValue=level;so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        foreach(var path in new[]{"Assets/Project/Prefabs/Player/Player (1).prefab","Assets/Project/Prefabs/Network/Player/NetPlayer.prefab"})
        {
            var before=File.ReadAllText(path);var player=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud=player.GetComponentInChildren<PlayerStressHUD>(true);
                if(!hud)hud=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),player.transform)).GetComponent<PlayerStressHUD>();
                var net=player.GetComponent<NetPlayer>();
                if(net){var so=new SerializedObject(net);var list=so.FindProperty("_localOnlyComponents");bool has=false;for(int i=0;i<list.arraySize;i++)has|=list.GetArrayElementAtIndex(i).objectReferenceValue==hud;
                    if(!has){list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=hud;}so.ApplyModifiedPropertiesWithoutUndo();}
                PrefabUtility.SaveAsPrefabAsset(player,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            string pattern=@"(?m)^--- !u!114 &(-?\d+)\r?\n[\s\S]*?(?=^--- !u!|\z)";
            var old=Regex.Matches(before,pattern).Cast<Match>().Where(m=>!m.Value.Contains("::NetPlayer\n")&&!m.Value.Contains("::NetPlayer\r")).ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
            File.WriteAllText(path,Regex.Replace(File.ReadAllText(path),pattern,m=>old.TryGetValue(m.Groups[1].Value,out var block)?block:m.Value));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static void Audit(string name)
    {
        var p=Player();var hud=p.GetComponentInChildren<PlayerStressHUD>(true);var so=new SerializedObject(hud);var stress=p.GetComponent<PlayerStressController>();
        var text=so.FindProperty("_valueText").objectReferenceValue as TMP_Text;var level=so.FindProperty("_levelText").objectReferenceValue as TMP_Text;
        var fill=so.FindProperty("_fill").objectReferenceValue as Image;
        File.WriteAllText(Out+"/"+name+".txt",$"playing={EditorApplication.isPlaying} stress={stress.CurrentStress} stage={stress.CurrentLevel}\nHUD count={p.GetComponentsInChildren<PlayerStressHUD>(true).Length} visible={hud.GetComponent<Canvas>().enabled} value={text.text} level={level.text} fill={fill.rectTransform.anchorMax.x} raycast={fill.raycastTarget}\n");
    }
    static void VerifyPrefabs()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var report="";
        foreach(var path in new[]{"Assets/Project/Prefabs/Player/Player (1).prefab","Assets/Project/Prefabs/Network/Player/NetPlayer.prefab"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var hud=root.GetComponentInChildren<PlayerStressHUD>(true);
            if(root.GetComponentsInChildren<PlayerStressHUD>(true).Length!=1)throw new Exception("Expected exactly one HUD: "+path);
            if(hud.GetComponent<Canvas>().enabled)throw new Exception("Canvas must start hidden until local owner binding");
            if(hud.GetComponentInChildren<GraphicRaycaster>(true))throw new Exception("HUD should not intercept input");
            var net=root.GetComponent<NetPlayer>();
            if(net){var list=new SerializedObject(net).FindProperty("_localOnlyComponents");bool found=false;for(int i=0;i<list.arraySize;i++)found|=list.GetArrayElementAtIndex(i).objectReferenceValue==hud;if(!found)throw new Exception("Missing local-only HUD");}
            report+="PASS "+path+" one HUD, initially hidden, no raycaster, local-only binding verified\n";
        }
        File.WriteAllText(Out+"/prefabs.txt",report);
    }
}
