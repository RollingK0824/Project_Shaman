using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mirror;
namespace Exorcist.OriginalRig.Editor
{
 public static class OriginalArmInstaller
 {
  const string Root="Assets/OriginalRightHand";
  public static void AddLayer(AnimatorController controller)
  {
   if(controller.layers.Any(l=>l.name==OriginalRightArm.LayerName))return;
   var originalLayers=controller.layers;var oldParameters=controller.parameters.Select(p=>p.name+":"+p.type).ToArray();
   var sm=new AnimatorStateMachine{name=OriginalRightArm.LayerName,hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(sm,controller);
   string[] states={"Empty","Equip","Hold","Use","Unequip"};
   for(int i=0;i<states.Length;i++){var s=sm.AddState(states[i],new Vector3(i*210,120));s.writeDefaultValues=false;if(i>0)s.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+states[i]+".anim");if(i==0)sm.defaultState=s;}
   // Timing is driven by the equipment presentation clock, so no locomotion parameters are reused.
   controller.AddLayer(new AnimatorControllerLayer{name=OriginalRightArm.LayerName,stateMachine=sm,avatarMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Root+"/Animations/RightArm.mask"),blendingMode=AnimatorLayerBlendingMode.Override,defaultWeight=0});
   for(int i=0;i<originalLayers.Length;i++)if(controller.layers[i].stateMachine!=originalLayers[i].stateMachine)throw new Exception("Original state machine changed.");
   if(!oldParameters.SequenceEqual(controller.parameters.Select(p=>p.name+":"+p.type)))throw new Exception("Original parameters changed.");
   EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
  }
  [MenuItem("Tools/Exorcist Original Rig/Add Right Hand Layer to Selected Player")]
  public static void Selected()
  {
   var selected=Selection.activeGameObject;if(!selected)throw new Exception("Select the existing Player prefab root.");
   string path=AssetDatabase.GetAssetPath(selected);if(!path.EndsWith(".prefab"))throw new Exception("Select a saved player prefab in the Project window.");
   InstallPath(path);
  }
  public static void InstallPath(string path)
  {
   var player=PrefabUtility.LoadPrefabContents(path);
   try
   {
    var animator=player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.isHuman&&a.avatar&&a.avatar.isValid);
    if(!animator)throw new Exception("Existing Humanoid Animator required.");
    var controller=animator.runtimeAnimatorController as AnimatorController;
    if(!controller)throw new Exception("This installer requires an AnimatorController; preserve and integrate existing OverrideControllers manually.");
    string backup=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"RightHand_Backups",DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(backup);
    File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));string controllerPath=AssetDatabase.GetAssetPath(controller);File.Copy(controllerPath,Path.Combine(backup,Path.GetFileName(controllerPath)));
    var renderers=player.GetComponentsInChildren<SkinnedMeshRenderer>(true);var meshes=renderers.Select(r=>r.sharedMesh).ToArray();var materials=renderers.Select(r=>r.sharedMaterials).ToArray();
    AddLayer(controller);
    var presentation=animator.GetComponent<OriginalRightArm>();if(!presentation)presentation=animator.gameObject.AddComponent<OriginalRightArm>();presentation.animator=animator;presentation.itemSet=AssetDatabase.LoadAssetAtPath<OriginalItemSet>(Root+"/Animations/OriginalItems.asset");
    var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var socket=hand.Find("OriginalRightHand_ItemSocket");if(!socket){socket=new GameObject("OriginalRightHand_ItemSocket").transform;socket.SetParent(hand,false);}presentation.handSocket=socket;
    var identity=player.GetComponent<NetworkIdentity>();if(identity)
    {
     var net=player.GetComponent<OriginalRightArmNetwork>();if(!net)net=player.AddComponent<OriginalRightArmNetwork>();net.presentation=presentation;
     var networkAnimator=player.GetComponent<NetworkAnimator>();if(networkAnimator){networkAnimator.animator=animator;var so=new SerializedObject(networkAnimator);var authority=so.FindProperty("clientAuthority");if(authority!=null)authority.boolValue=true;var direction=so.FindProperty("syncDirection");if(direction!=null)direction.enumValueIndex=1;so.ApplyModifiedPropertiesWithoutUndo();net.usesNetworkAnimator=true;}else net.usesNetworkAnimator=false;
     var bridgeType=TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t=>t.Name=="OriginalRightHandBridge");
     if(bridgeType!=null){var bridge=player.GetComponent(bridgeType);if(!bridge)bridge=player.AddComponent(bridgeType);var so=new SerializedObject(bridge);so.FindProperty("_network").objectReferenceValue=net;so.FindProperty("_controller").objectReferenceValue=player.GetComponent("PlayerItemController");so.ApplyModifiedPropertiesWithoutUndo();var items=player.GetComponent("PlayerItemController");if(items){var cso=new SerializedObject(items);var field=cso.FindProperty("_originalRig");if(field!=null){field.objectReferenceValue=bridge;cso.ApplyModifiedPropertiesWithoutUndo();}}}
    }
    for(int i=0;i<renderers.Length;i++){if(renderers[i].sharedMesh!=meshes[i]||!renderers[i].sharedMaterials.SequenceEqual(materials[i]))throw new Exception("Appearance reference changed.");}
    PrefabUtility.SaveAsPrefabAsset(player,path);Debug.Log("Original mesh/material/avatar and locomotion preserved. Added right-hand layer to "+path+". Backup: "+backup);
   }
   finally{PrefabUtility.UnloadPrefabContents(player);}
  }
 }
}
