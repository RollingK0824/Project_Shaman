using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class StressEditorSession
{
    const string Work="C:/Users/inha/Documents/Codex/2026-10-01/new-chat/work/stress-system";
    const string Out="C:/Users/inha/Documents/Codex/2026-10-01/new-chat/outputs/StressSystem";
    const string Prefab="Assets/Project/Prefabs/Player/Player (1).prefab";
    const string Profile="Assets/Project/Settings/StressVolumeProfile.asset";
    [Serializable] public class Request { public string id; public string action; public string[] args; }
    [Serializable] class Result { public string id; public bool ok; public string message; }
    static double releaseAt; static bool releasePending;
    static StressEditorSession(){EditorApplication.update+=Poll;EditorApplication.update+=Release;EditorApplication.delayCall+=()=>File.WriteAllText(Work+"/ready.txt",DateTime.Now.ToString("O"));}
    static void Poll(){var path=Work+"/request.json";if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;var r=JsonUtility.FromJson<Request>(File.ReadAllText(path));File.Delete(path);try{Run(r);File.WriteAllText(Work+"/done.json",JsonUtility.ToJson(new Result{id=r.id,ok=true}));}catch(Exception e){File.WriteAllText(Work+"/done.json",JsonUtility.ToJson(new Result{id=r.id,ok=false,message=e.ToString()}));}}
    static GameObject Player()=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Stress_LocalTest_Player");
    static void Run(Request r){var a=r.args??Array.Empty<string>();switch(r.action){
        case "audit":Audit();break;case "install":Install();break;case "tests":Tests();break;
        case "prepare":Prepare();break;case "play":EditorApplication.isPlaying=true;break;
        case "activate":Player().SetActive(true);EditorApplication.isPaused=false;Focus();break;
        case "set":Player().GetComponent<PlayerStressController>().SetStress(float.Parse(a[0],System.Globalization.CultureInfo.InvariantCulture));Focus();break;
        case "zone":Zone(a[0]);break;case "snapshot":Snapshot(a[0]);break;case "capture":Capture(a[0]);break;
        case "input":Input(a[0]);break;case "first":var view=Player().GetComponent<PlayerViewModeController>();if(view.CurrentViewMode!=PlayerViewModeController.ViewMode.FirstPerson)view.ToggleViewMode();Player().GetComponent<PlayerCameraController>().CanLook=false;Player().transform.Find("CameraPivot").localRotation=Quaternion.identity;Focus();break;
        case "damage":Player().GetComponent<PlayerHealth>().ReceiveDamage(5f);Focus();break;
        case "ghost":Player().GetComponent<IStressReceiver>().ReceiveStress(20f,StressCause.GhostSight);Focus();break;
        case "stop":EditorApplication.isPlaying=false;break;case "cleanup":Cleanup();break;default:throw new Exception(r.action);
    }}
    static void Focus()=>EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
    static void Input(string value){Focus();EditorApplication.isPaused=false;var keys=value=="walk"?new[]{Key.W}:value=="run"?new[]{Key.W,Key.LeftShift}:new[]{value=="f"?Key.F:Key.Digit1};InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));releaseAt=EditorApplication.timeSinceStartup+(value=="walk"||value=="run"?1.2:.15);releasePending=true;}
    static void Release(){if(releasePending&&EditorApplication.timeSinceStartup>=releaseAt){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());releasePending=false;}}
    static void Ref(SerializedObject so,string field,UnityEngine.Object value)=>so.FindProperty(field).objectReferenceValue=value;
    static void Install(){if(EditorApplication.isPlaying)throw new Exception("Stop play before install");AssetDatabase.Refresh();
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Profile);if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Profile);}
        if(!profile.TryGet<Vignette>(out var vignette)){vignette=profile.Add<Vignette>(true);AssetDatabase.AddObjectToAsset(vignette,profile);}
        vignette.color.Override(Color.black);vignette.intensity.Override(.48f);vignette.smoothness.Override(.68f);vignette.rounded.Override(true);EditorUtility.SetDirty(vignette);EditorUtility.SetDirty(profile);
        var before=File.ReadAllText(Prefab);File.WriteAllText(Work+"/Player-before.prefab",before);var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{var feedback=root.GetComponent<PlayerFeedbackController>();var cam=root.GetComponentInChildren<Camera>(true);if(!feedback||!cam)throw new Exception("Existing feedback/camera missing");
            var holder=cam.transform.Find("StressFeedback");if(!holder){holder=new GameObject("StressFeedback").transform;holder.SetParent(cam.transform,false);}
            var volume=holder.GetComponent<Volume>();if(!volume)volume=holder.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=50;volume.weight=0;volume.sharedProfile=profile;
            AudioSource MakeAudio(string name,string clip){var t=holder.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(holder,false);}var source=t.GetComponent<AudioSource>();if(!source)source=t.gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Audio/Stress/"+clip);source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=0;return source;}
            var heart=MakeAudio("Heartbeat","Stress_Heartbeat.wav");var breath=MakeAudio("Breathing","Stress_Breathing.wav");
            var so=new SerializedObject(feedback);Ref(so,"_feedbackCamera",cam);Ref(so,"_stressVolume",volume);Ref(so,"_heartbeatSource",heart);Ref(so,"_breathingSource",breath);Ref(so,"_handFeedbackPivot",root.GetComponentInChildren<Exorcist.FirstPerson.FirstPersonRightHand>(true).transform);so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        // Mirror OnValidate may rewrite unrelated serialized components while
        // loading prefab contents. Preserve all pre-existing behaviour blocks
        // except the two components deliberately configured here.
        string pattern=@"(?m)^--- !u!114 &(-?\d+)\r?\n[\s\S]*?(?=^--- !u!|\z)";
        var prior=Regex.Matches(before,pattern).Cast<Match>().Where(m=>!m.Value.Contains("::PlayerFeedbackController")&&!m.Value.Contains("::PlayerStressController")).ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
        var saved=Regex.Replace(File.ReadAllText(Prefab),pattern,m=>prior.TryGetValue(m.Groups[1].Value,out string old)?old:m.Value);File.WriteAllText(Prefab,saved);AssetDatabase.ImportAsset(Prefab,ImportAssetOptions.ForceUpdate);
        var scene=SceneManager.GetActiveScene();var anomaly=scene.GetRootGameObjects().Single(g=>g.name=="AnomalyStressTest").GetComponent<StressEventTrigger>();var aso=new SerializedObject(anomaly);aso.FindProperty("_cause").enumValueIndex=(int)StressCause.AnomalySight;aso.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Audit();
    }
    static void Audit(){var report=new StringBuilder();foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()){var f=root.GetComponent<PlayerFeedbackController>();if(!f)continue;var so=new SerializedObject(f);report.AppendLine(root.name);foreach(var key in new[]{"_feedbackCamera","_handFeedbackPivot","_stressVolume","_heartbeatSource","_breathingSource","_hitVolume"})report.AppendLine(key+"="+so.FindProperty(key).objectReferenceValue);}File.WriteAllText(Out+"/references.txt",report.ToString());var s=new StringBuilder();s.AppendLine($"playing={EditorApplication.isPlaying} paused={EditorApplication.isPaused}");var scene=SceneManager.GetActiveScene();s.AppendLine(scene.path+" dirty="+scene.isDirty);foreach(var player in scene.GetRootGameObjects().Where(g=>g.GetComponent<PlayerStressController>()))s.AppendLine(player.name+" active="+player.activeSelf+" feedback="+EditorJsonUtility.ToJson(player.GetComponent<PlayerFeedbackController>()));foreach(var zone in UnityEngine.Object.FindObjectsByType<StressZone>(FindObjectsInactive.Include,FindObjectsSortMode.None))s.AppendLine(zone.name+" "+EditorJsonUtility.ToJson(zone));File.WriteAllText(Out+"/audit.txt",s.ToString());}
    static void Prepare(){if(EditorApplication.isPlaying)throw new Exception("Stop first");var scene=SceneManager.GetActiveScene();if(!scene.path.EndsWith("PlayerPrototype.unity"))throw new Exception("Wrong scene");var original=scene.GetRootGameObjects().Single(g=>g.name=="Player (1)");SessionState.SetBool("StressOriginalActive",original.activeSelf);original.SetActive(false);var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);obj.name="Stress_LocalTest_Player";obj.transform.SetPositionAndRotation(new Vector3(0,0,-8),Quaternion.identity);Selection.activeGameObject=obj;}
    static void Zone(string name){var player=Player();var cc=player.GetComponent<CharacterController>();cc.enabled=false;if(name=="outside")player.transform.position=new Vector3(0,0,-8);else{var target=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name==name).GetComponent<Collider>().bounds;player.transform.position=new Vector3(target.center.x,target.min.y+.05f,target.center.z);}cc.enabled=true;Physics.SyncTransforms();Focus();}
    static void Snapshot(string name){var p=Player();var stress=p.GetComponent<PlayerStressController>();var health=p.GetComponent<PlayerHealth>();var f=p.GetComponent<PlayerFeedbackController>();var so=new SerializedObject(f);var s=new StringBuilder();s.AppendLine($"frame={Time.frameCount} stress={stress.CurrentStress} level={stress.CurrentLevel} intensity={f.StressFeedbackIntensity} hp={health.CurrentHealth} dead={health.IsDead}");s.AppendLine("position="+p.transform.position);var rates=(System.Collections.IDictionary)typeof(PlayerStressController).GetField("_sourceRates",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(stress);foreach(System.Collections.DictionaryEntry rate in rates)s.AppendLine("source="+rate.Key+" rate="+rate.Value);foreach(var key in new[]{"_stressVolume","_hitVolume","_heartbeatSource","_breathingSource","_handFeedbackPivot","_feedbackPivot"}){var v=so.FindProperty(key).objectReferenceValue;s.AppendLine(key+"="+v);if(v is Volume volume)s.AppendLine(" weight="+volume.weight);if(v is AudioSource audio)s.AppendLine($" playing={audio.isPlaying} volume={audio.volume} pitch={audio.pitch} clip={audio.clip}");if(v is Transform t)s.AppendLine($" position={t.localPosition.ToString("F6")} rotation={t.localEulerAngles.ToString("F4")}");}var an=p.GetComponentInChildren<Animator>();if(an)s.AppendLine("speed="+an.GetFloat("Speed"));File.WriteAllText(Out+"/"+name+".txt",s.ToString());}
    static void Capture(string name){var cam=Player().GetComponentInChildren<Camera>();var rt=new RenderTexture(1280,720,24);var old=cam.targetTexture;var active=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);ScreenCapture.CaptureScreenshot(Out+"/"+name+"-GameView.png");Snapshot(name);}
    static void Cleanup(){if(EditorApplication.isPlaying)throw new Exception("Stop first");var scene=SceneManager.GetActiveScene();foreach(var g in scene.GetRootGameObjects().Where(g=>g.name=="Stress_LocalTest_Player").ToArray())UnityEngine.Object.DestroyImmediate(g);var original=scene.GetRootGameObjects().Single(g=>g.name=="Player (1)");original.SetActive(SessionState.GetBool("StressOriginalActive",true));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Audit();}
    static TestRunnerApi runner;
    static void Tests(){runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,assemblyNames=new[]{"Project.Stress.Tests"}}));}
    class Results:ICallbacks{public void RunStarted(ITestAdaptor testsToRun){}public void TestStarted(ITestAdaptor test){}public void TestFinished(ITestResultAdaptor result){File.AppendAllText(Out+"/tests.log",result.FullName+" "+result.TestStatus+" "+result.Message+"\n");}public void RunFinished(ITestResultAdaptor result){TestRunnerApi.SaveResultToFile(result,Out+"/test-results.xml");File.WriteAllText(Out+"/tests-done.txt",$"passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n{result.Message}");}}
}
