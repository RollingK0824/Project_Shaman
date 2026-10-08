using System;
using UnityEngine;
namespace Exorcist.OriginalRig
{
 [DefaultExecutionOrder(-10)]
 public sealed class OriginalRightArm:MonoBehaviour
 {
  public const string LayerName="Right Hand Items";
  public Animator animator;public Transform handSocket;public OriginalItemSet itemSet;
  public ushort VisibleId{get;private set;}
  public GameObject Visual{get;private set;}
  public string Phase{get;private set;}="Empty";
  public event Action<ushort> VisualChanged;
  ushort oldId,nextId;double started,useStarted;bool initialized,usingItem;int layer=-1;bool externalTick;
  public bool IsTransitioning(double now)=>now<started+(oldId!=0?.32:0)+(nextId!=0?.38:.15);
  public void Initialize(){if(initialized)return;if(!animator)animator=GetComponent<Animator>();layer=animator.GetLayerIndex(LayerName);if(layer<0)throw new InvalidOperationException("Install the Right Hand Items Animator layer first.");initialized=true;}
  public void SetPlan(ushort previous,ushort next,double time,bool snap=false)
  {
   Initialize();oldId=previous;nextId=next;started=time;usingItem=false;
   if(snap){oldId=nextId;started=time-1;Replace(nextId);}
  }
  public void SetUse(bool active,double time){usingItem=active;useStarted=time;}
  public void RequestLocal(ushort id){if(id!=0&&itemSet.Get(id)==null)return;SetPlan(VisibleId,id,Time.timeAsDouble);}
  public void UseLocal(bool active){SetUse(active,Time.timeAsDouble);}
  void Update(){if(!externalTick)Tick(Time.timeAsDouble,true);}
  public void UseExternalClock(){externalTick=true;}
  public void Tick(double now,bool driveAnimator)
  {
   Initialize();double lower=oldId!=0?.32:0;double t=now-started;float weight=0,normalized=0;string state="Empty";
   if(t<lower){Replace(oldId);state="Unequip";normalized=Mathf.Clamp01((float)(t/.32));weight=1;}
   else
   {
    Replace(nextId);double raised=t-lower;
    if(nextId==0&&oldId!=0&&raised<.15){state="Unequip";normalized=1;weight=1-Mathf.SmoothStep(0,1,(float)(raised/.15));}
    if(nextId!=0)
    {
     if(raised<.38){state="Equip";normalized=(float)(raised/.38);weight=oldId==0?Mathf.SmoothStep(0,1,normalized):1;}
     else{state=usingItem?"Use":"Hold";normalized=usingItem?(float)((now-useStarted)/.72%1):(float)(raised/2%1);weight=1;}
    }
   }
   Phase=state;
   if(driveAnimator){animator.SetLayerWeight(layer,weight);if(weight>0)animator.Play(Animator.StringToHash(LayerName+"."+state),layer,normalized);}
  }
  void Replace(ushort id)
  {
   if(VisibleId==id)return;
   if(Visual){Visual.SetActive(false);Destroy(Visual);}Visual=null;VisibleId=id;
   var item=itemSet.Get(id);
   if(item!=null&&item.visualPrefab)
   {
    Visual=Instantiate(item.visualPrefab,handSocket,false);Visual.name=item.label+"_RightHand";Visual.transform.localPosition=item.position;Visual.transform.localRotation=Quaternion.Euler(item.eulerAngles);Visual.transform.localScale=Vector3.Scale(item.visualPrefab.transform.localScale,item.scale);
   }
   VisualChanged?.Invoke(id);
  }
  void OnDisable(){if(initialized&&animator&&layer>=0)animator.SetLayerWeight(layer,0);}
 }
}
