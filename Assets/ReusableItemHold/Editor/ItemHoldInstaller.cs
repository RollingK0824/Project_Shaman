using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace ProjectShaman.ItemHold.Editor
{
 public static class ItemHoldInstaller
 {
  public const string Root="Assets/ReusableItemHold";
  public static void AddLayer(AnimatorController controller)
  {
   if(controller.layers.Any(l=>l.name==ItemHoldDriver.LayerName))return;
   var parameter=controller.parameters.FirstOrDefault(p=>p.name==ItemHoldDriver.TypeParameter);
   if(parameter!=null&&parameter.type!=AnimatorControllerParameterType.Int)throw new InvalidOperationException("ItemHoldType parameter must be Int.");
   if(parameter==null)controller.AddParameter(ItemHoldDriver.TypeParameter,AnimatorControllerParameterType.Int);
   var machine=new AnimatorStateMachine{name=ItemHoldDriver.LayerName,hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(machine,controller);
   string[] names={"Empty","Equip","Hold_OneHandSmall","Hold_OneHandVertical","Hold_OneHandFront","Hold_OneHandPalm","Hold_BookHold","UseBell","UseObserve","Unequip"};
   for(int i=0;i<names.Length;i++){var s=machine.AddState(names[i],new Vector3((i%4)*230,(i/4)*120));s.writeDefaultValues=false;if(i==0)machine.defaultState=s;else s.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+names[i]+".anim");}
   controller.AddLayer(new AnimatorControllerLayer{name=ItemHoldDriver.LayerName,stateMachine=machine,defaultWeight=0,blendingMode=AnimatorLayerBlendingMode.Override,avatarMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Root+"/Animations/RightUpperBody.mask")});EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
  }
  [MenuItem("Tools/Project Shaman/Add Reusable Item Hold to Selected Prefab")]
  public static void Selected()
  {
   string path=AssetDatabase.GetAssetPath(Selection.activeGameObject);if(!path.EndsWith(".prefab"))throw new InvalidOperationException("Select the existing player prefab in the Project window.");Install(path);
  }
  public static void Install(string path)
  {
   string originalText=File.ReadAllText(path);
   var root=PrefabUtility.LoadPrefabContents(path);
   try
   {
    var animator=root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.isHuman&&a.avatar&&a.avatar.isValid);
    if(!animator||!(animator.runtimeAnimatorController is AnimatorController controller))throw new InvalidOperationException("Existing Humanoid AnimatorController required.");
    string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"ItemHold_Backups",DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(folder);File.Copy(path,Path.Combine(folder,Path.GetFileName(path)));File.Copy(AssetDatabase.GetAssetPath(controller),Path.Combine(folder,Path.GetFileName(AssetDatabase.GetAssetPath(controller))));
    AddLayer(controller);var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var socket=hand.Find("RightHand_ItemSocket");if(!socket){socket=new GameObject("RightHand_ItemSocket").transform;socket.SetParent(hand,false);}
    var driver=animator.GetComponent<ItemHoldDriver>();if(!driver)driver=animator.gameObject.AddComponent<ItemHoldDriver>();driver.Configure(animator,socket);PrefabUtility.SaveAsPrefabAsset(root,path);
    // Loading prefab contents can invoke third-party OnValidate methods. Preserve all
    // pre-existing component serialization instead of accepting unrelated side effects.
    string pattern=@"(?m)^--- !u!114 &(-?\d+)\r?\n[\s\S]*?(?=^--- !u!|\z)";
    var existing=Regex.Matches(originalText,pattern).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
    string saved=File.ReadAllText(path);saved=Regex.Replace(saved,pattern,m=>existing.TryGetValue(m.Groups[1].Value,out string prior)?prior:m.Value);
    File.WriteAllText(path,saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    Debug.Log("Added local Item Hold layer and socket. Existing model, Base Layer and network components retained. Backup: "+folder);
   }
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
 }
}
