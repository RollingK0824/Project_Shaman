using Mirror;
using UnityEngine;
namespace Exorcist.OriginalRig
{
 public struct ArmPlan{public ushort previous,next;public uint revision;public double started;}
 public struct ArmUse{public bool active;public double started;}
 [DefaultExecutionOrder(-20)]
 public sealed class OriginalRightArmNetwork:NetworkBehaviour
 {
  public OriginalRightArm presentation;
  [Tooltip("Enable when the existing owner-authoritative NetworkAnimator synchronizes this Animator.")]
  public bool usesNetworkAnimator=true;
  [SyncVar(hook=nameof(OnPlan))]ArmPlan plan;
  [SyncVar(hook=nameof(OnUse))]ArmUse use;
  // Cosmetic presentation only. Gameplay ownership / inventory remains with the game's server logic.
  public System.Func<ushort,bool> ServerCanPresent;
  ushort pendingId;bool pending,ready;double nextAllowed;
  public ushort EquippedId=>plan.next;public bool IsUsing=>use.active;
  void Awake(){presentation.UseExternalClock();}
  public override void OnStartClient(){ready=true;presentation.SetPlan(plan.next,plan.next,NetworkTime.time,true);presentation.SetUse(use.active,use.started);}
  void OnPlan(ArmPlan old,ArmPlan next){if(ready)presentation.SetPlan(next.previous,next.next,next.started);}
  void OnUse(ArmUse old,ArmUse next){if(ready)presentation.SetUse(next.active,next.started);}
  public void RequestItem(ushort id){if(!NetworkClient.active&&!NetworkServer.active){presentation.RequestLocal(id);return;}if(isOwned)CmdPresent(id);}
  public void RequestUse(bool active){if(!NetworkClient.active&&!NetworkServer.active){presentation.UseLocal(active);return;}if(isOwned)CmdUse(active);}
  [Command]void CmdPresent(ushort id){if(id!=0&&presentation.itemSet.Get(id)==null)return;if(ServerCanPresent!=null&&!ServerCanPresent(id))return;pendingId=id;pending=true;}
  [Command]void CmdUse(bool active){if(active&&(NetworkTime.time<nextAllowed||presentation.itemSet.Get(plan.next)?.canShake!=true))return;use=new ArmUse{active=active,started=NetworkTime.time};}
  [Server]public void ServerSetItem(ushort id){if(id!=0&&presentation.itemSet.Get(id)==null)return;pendingId=id;pending=true;}
  void Update()
  {
   if(isServer&&pending&&NetworkTime.time>=nextAllowed)
   {
    pending=false;if(pendingId!=plan.next){use=new ArmUse();var prev=plan.next;plan=new ArmPlan{previous=prev,next=pendingId,revision=plan.revision+1,started=NetworkTime.time};nextAllowed=NetworkTime.time+(prev!=0?.32:0)+(pendingId!=0?.38:.15);}
   }
   if(!NetworkClient.active&&!NetworkServer.active){presentation.Tick(Time.timeAsDouble,true);return;}
   if(isClient&&ready)presentation.Tick(NetworkTime.time,!usesNetworkAnimator||isOwned);
  }
 }
}
