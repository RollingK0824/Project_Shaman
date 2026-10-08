using UnityEngine;
namespace Exorcist.OriginalRig
{
 // Review-scene controls only. Do not add this component to a network player.
 public sealed class OriginalArmPreview:MonoBehaviour
 {
  public OriginalRightArm arm;bool shaking;
  void Start(){arm.animator.SetBool("Grounded",true);arm.animator.SetFloat("MotionSpeed",1);arm.animator.Play("Base Layer.Idle Walk Run Blend",0,0);}
  void OnGUI()
  {
   GUILayout.BeginArea(new Rect(12,12,225,330),GUI.skin.box);GUILayout.Label("Original character / repaired hands");
   if(GUILayout.Button("Equip bell"))arm.RequestLocal(1);
   if(GUILayout.Button("Unequip"))arm.RequestLocal(0);
   bool next=GUILayout.RepeatButton("Hold to shake");if(next!=shaking){shaking=next;arm.UseLocal(next);}
   if(GUILayout.Button("Idle"))arm.animator.SetFloat("Speed",0);
   if(GUILayout.Button("Walk"))arm.animator.SetFloat("Speed",2);
   if(GUILayout.Button("Run"))arm.animator.SetFloat("Speed",6);
   GUILayout.Label(arm.Phase);GUILayout.EndArea();
  }
  public void OnFootstep(AnimationEvent e){}public void OnLand(AnimationEvent e){}
 }
}
