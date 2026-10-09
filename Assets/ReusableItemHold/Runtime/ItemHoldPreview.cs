using UnityEngine;
namespace ProjectShaman.ItemHold
{
// Standalone review scene only; no gameplay or network integration.
public sealed class ItemHoldPreview : MonoBehaviour
{
 public ItemHoldDriver driver;GameObject prop;int selected=2;bool waiting;
 void Start(){driver.Unequipped+=Clear;var a=driver.Animator;a.SetBool("Grounded",true);a.SetFloat("MotionSpeed",1);a.SetFloat("Speed",0);a.Play("Base Layer.Idle Walk Run Blend",0,0);}
 void Clear(){if(prop){prop.SetActive(false);Destroy(prop);}prop=null;}
 void LateUpdate(){if(waiting&&driver.Phase==HoldPhase.Hold&&!driver.Animator.IsInTransition(driver.Animator.GetLayerIndex(ItemHoldDriver.LayerName))){waiting=false;BuildProp(selected);}}
 void OnGUI(){GUILayout.BeginArea(new Rect(12,12,245,440),GUI.skin.box);GUILayout.Label("Reusable right-hand Hold / local preview");for(int i=1;i<=5;i++)if(GUILayout.Button(((HoldType)i).ToString())){selected=i;Clear();driver.SetHoldType(i);driver.Equip();waiting=true;}if(GUILayout.Button("Unequip")){waiting=false;driver.Unequip();}if(GUILayout.Button("Use"))driver.Use();if(GUILayout.Button("Idle"))driver.Animator.SetFloat("Speed",0);if(GUILayout.Button("Walk"))driver.Animator.SetFloat("Speed",2);if(GUILayout.Button("Run"))driver.Animator.SetFloat("Speed",6);GUILayout.Label(driver.Phase.ToString());GUILayout.EndArea();}
 public void OnFootstep(AnimationEvent e){}public void OnLand(AnimationEvent e){}
 void BuildProp(int type)
 {
  if(prop){prop.SetActive(false);Destroy(prop);}var a=driver.Animator;var w=a.GetBoneTransform(HumanBodyBones.RightHand);Vector3 f=(a.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position-w.position).normalized;Vector3 u=(a.GetBoneTransform(HumanBodyBones.RightIndexProximal).position-a.GetBoneTransform(HumanBodyBones.RightLittleProximal).position).normalized;Vector3 n=Vector3.Cross(f,u).normalized;
  var mid=a.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);var index=a.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);Vector3 center=Vector3.Lerp(mid.position,index.position,.5f)-f*.006f;
  if(type==1)center=(a.GetBoneTransform(HumanBodyBones.RightIndexDistal).GetChild(0).position+a.GetBoneTransform(HumanBodyBones.RightThumbDistal).GetChild(0).position)*.5f;
  if(type>=4)center=w.position+f*.045f+n*.016f;
  prop=new GameObject("Arbitrary test prefab "+type);prop.transform.SetParent(driver.ItemSocket,false);prop.transform.SetPositionAndRotation(center,Quaternion.LookRotation(-n,u));prop.transform.localScale=Vector3.one/w.lossyScale.x;
  if(type==1){Part(PrimitiveType.Cylinder,Vector3.zero,new Vector3(.014f,.0015f,.014f),new Color(.85f,.7f,.25f));prop.transform.GetChild(0).localRotation=Quaternion.Euler(90,0,0);}
  if(type==2)Part(PrimitiveType.Cylinder,Vector3.zero,new Vector3(.014f,.065f,.014f),new Color(.45f,.23f,.09f));
  if(type==3){Part(PrimitiveType.Cylinder,Vector3.zero,new Vector3(.012f,.035f,.012f),new Color(.4f,.24f,.1f));Part(PrimitiveType.Cube,new Vector3(0,.061f,0),new Vector3(.062f,.064f,.006f),new Color(.6f,.78f,.83f));}
  if(type==4)Part(PrimitiveType.Cube,Vector3.zero,new Vector3(.035f,.035f,.009f),new Color(.8f,.62f,.2f));
  if(type==5)Part(PrimitiveType.Cube,new Vector3(0,0,-.006f),new Vector3(.07f,.09f,.012f),new Color(.26f,.10f,.07f));
 }
 void Part(PrimitiveType shape,Vector3 position,Vector3 scale,Color color){var g=GameObject.CreatePrimitive(shape);g.transform.SetParent(prop.transform,false);g.transform.localPosition=position;g.transform.localScale=scale;Destroy(g.GetComponent<Collider>());var material=new Material(Shader.Find("Standard")){color=color};g.GetComponent<Renderer>().material=material;}
}
}
