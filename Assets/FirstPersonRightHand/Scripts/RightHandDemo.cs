using UnityEngine;
namespace Exorcist.FirstPerson
{
    public sealed class RightHandDemo : MonoBehaviour
    {
        public FirstPersonRightHand hand;
        public HandItemProfile bell, cylinder, pinch;
        public bool shake;
        Vector3 origin;Quaternion rotation;float clock;
        void Start(){origin=hand.transform.localPosition;rotation=hand.transform.localRotation;}
        void Update()
        {
            clock+=Time.deltaTime;
            float wave=shake?Mathf.Sin(clock*18):0;
            hand.transform.localPosition=origin+new Vector3(wave*.012f,Mathf.Sin(clock*1.8f)*.0015f,0);
            hand.transform.localRotation=rotation*Quaternion.Euler(0,wave*4,wave*5);
        }
        void OnGUI()
        {
            GUI.Box(new Rect(20,20,440,116),"RIGHT HAND / interchangeable item grip");
            if(GUI.Button(new Rect(32,52,92,30),"Bell"))hand.Equip(bell);
            if(GUI.Button(new Rect(128,52,100,30),"Cylinder"))hand.Equip(cylinder);
            if(GUI.Button(new Rect(232,52,100,30),"Pinch"))hand.Equip(pinch);
            if(GUI.Button(new Rect(336,52,110,30),"Empty hand"))hand.Unequip();
            shake=GUI.Toggle(new Rect(34,94,300,25),shake,"Shake held item");
        }
    }
}
