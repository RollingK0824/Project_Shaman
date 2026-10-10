using System;
using UnityEngine;
namespace Exorcist.OriginalRig
{
 [CreateAssetMenu(menuName="Exorcist/Original Rig Item Set")]
 public sealed class OriginalItemSet:ScriptableObject
 {
  [Serializable]public sealed class Item
  {
   public ushort id;public string label;public UnityEngine.Object gameplayItem;public GameObject visualPrefab;
   public Vector3 position,eulerAngles,scale=Vector3.one;public bool canShake;
  }
  public Item[] items;
  public Item Get(ushort id){if(items!=null)foreach(var item in items)if(item.id==id&&id!=0)return item;return null;}
  public ushort Find(UnityEngine.Object data){if(data&&items!=null)foreach(var i in items)if(i.gameplayItem==data)return i.id;return 0;}
 }
}
