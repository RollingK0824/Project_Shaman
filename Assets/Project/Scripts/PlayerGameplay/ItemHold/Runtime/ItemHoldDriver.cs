using System;
using UnityEngine;
namespace ProjectShaman.ItemHold
{
 public enum HoldType { Empty, OneHandSmall, OneHandVertical, OneHandFront, OneHandPalm, BookHold }
 public enum HoldPhase { Empty, Equip, Hold, Use, Unequip }
 [DisallowMultipleComponent]
 [DefaultExecutionOrder(30)]
 public sealed class ItemHoldDriver : MonoBehaviour
 {
  public const string LayerName="Upper Body Item Layer";
  public const string TypeParameter="ItemHoldType";
  [SerializeField] Animator animator;
  [SerializeField] Transform itemSocket;
  [SerializeField,Min(.05f)] float poseBlendSeconds=.26f;
  [SerializeField] UseMotion[] useMotions={new UseMotion{ id=1,state="UseBell",duration=.72f },new UseMotion{ id=2,state="UseObserve",duration=1.15f }};
  [Serializable] public struct UseMotion { public int id;public string state;public float duration; }
  public Animator Animator=>animator;
  public Transform ItemSocket=>itemSocket;
  public HoldType CurrentHoldType {get;private set;}
  public HoldPhase Phase {get;private set;}
  public bool IsEquipped=>Phase==HoldPhase.Hold||Phase==HoldPhase.Use;
  public event Action Equipped, Unequipped, UseCompleted;
  public event Action<int> UseRequested;
  public event Action<int> HoldTypeChanged;
  int layer=-1;float elapsed,weightAtStart,useDuration;bool initialized,holdEntered,wantsEquip;
  const float EquipDuration=.55f,UnequipDuration=.55f;
  public void Configure(Animator target,Transform socket){animator=target;itemSocket=socket;initialized=false;}
  void Awake(){Initialize();}
  void Initialize()
  {
   if(initialized)return;
   if(!animator)animator=GetComponent<Animator>();
   if(!animator)throw new InvalidOperationException("An existing Animator is required.");
   layer=animator.GetLayerIndex(LayerName);
   if(layer<0)throw new InvalidOperationException("Install the Upper Body Item Layer first.");
   if(!itemSocket)itemSocket=animator.GetBoneTransform(HumanBodyBones.RightHand)?.Find("RightHand_ItemSocket");
   initialized=true;Phase=HoldPhase.Empty;animator.SetLayerWeight(layer,0);
  }
  public void SetHoldType(int type)
  {
   if(type<0||type>5)throw new ArgumentOutOfRangeException(nameof(type));
   Initialize();var next=(HoldType)type;if(next==CurrentHoldType)return;
   CurrentHoldType=next;animator.SetInteger(TypeParameter,type);HoldTypeChanged?.Invoke(type);
   if(next==HoldType.Empty){Unequip();return;}
   if(Phase==HoldPhase.Hold||Phase==HoldPhase.Use){Phase=HoldPhase.Hold;elapsed=0;BlendToHold();}
   else if(Phase==HoldPhase.Equip&&holdEntered)BlendToHold();
  }
  public void Equip()
  {
   Initialize();if(CurrentHoldType==HoldType.Empty)return;
   wantsEquip=true;if(Phase==HoldPhase.Unequip)return;
   if(Phase!=HoldPhase.Empty)return;
   BeginEquip();
  }
  void BeginEquip(){Phase=HoldPhase.Equip;elapsed=0;holdEntered=false;weightAtStart=animator.GetLayerWeight(layer);animator.Play(LayerName+".Equip",layer,0);}
  public void Unequip()
  {
   Initialize();wantsEquip=false;if(Phase==HoldPhase.Empty||Phase==HoldPhase.Unequip)return;
   Phase=HoldPhase.Unequip;elapsed=0;weightAtStart=animator.GetLayerWeight(layer);animator.CrossFadeInFixedTime(LayerName+".Unequip",.18f,layer,0);
  }
  public void Use(){Use(CurrentHoldType==HoldType.OneHandVertical?1:CurrentHoldType==HoldType.OneHandFront?2:0);}
  public void Use(int useId)
  {
   Initialize();if(Phase!=HoldPhase.Hold)return;UseRequested?.Invoke(useId);
   foreach(var motion in useMotions)if(motion.id==useId&&useId!=0)
   {
    if(!animator.HasState(layer,Animator.StringToHash(LayerName+"."+motion.state)))return;
    Phase=HoldPhase.Use;elapsed=0;useDuration=Mathf.Max(.15f,motion.duration);animator.CrossFadeInFixedTime(LayerName+"."+motion.state,.14f,layer,0);return;
   }
   UseCompleted?.Invoke();
  }
  void BlendToHold(){animator.CrossFadeInFixedTime(LayerName+".Hold_"+CurrentHoldType,poseBlendSeconds,layer,0);}
  void Update()
  {
   Initialize();int requested=animator.GetInteger(TypeParameter);if(requested!=(int)CurrentHoldType)SetHoldType(requested);
   elapsed+=Time.deltaTime;
   if(Phase==HoldPhase.Equip)
   {
    animator.SetLayerWeight(layer,Mathf.Lerp(weightAtStart,1,Mathf.SmoothStep(0,1,elapsed/.42f)));
    if(!holdEntered&&elapsed>=.26f){holdEntered=true;BlendToHold();}
    if(elapsed>=EquipDuration){Phase=HoldPhase.Hold;animator.SetLayerWeight(layer,1);Equipped?.Invoke();}
   }
   else if(Phase==HoldPhase.Unequip)
   {
    animator.SetLayerWeight(layer,weightAtStart*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.10f)/.45f))));
    if(elapsed>=UnequipDuration){Phase=HoldPhase.Empty;animator.SetLayerWeight(layer,0);Unequipped?.Invoke();if(Phase==HoldPhase.Empty&&wantsEquip&&CurrentHoldType!=HoldType.Empty)BeginEquip();}
   }
   else if(Phase==HoldPhase.Use&&elapsed>=useDuration){Phase=HoldPhase.Hold;BlendToHold();UseCompleted?.Invoke();}
  }
  void OnDisable(){if(initialized&&animator){animator.SetLayerWeight(layer,0);Phase=HoldPhase.Empty;wantsEquip=false;}}
 }
}
