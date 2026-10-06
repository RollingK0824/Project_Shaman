using System;
using UnityEngine;
namespace Exorcist.FirstPerson
{
    [DefaultExecutionOrder(100)]
    public sealed class FirstPersonRightHand : MonoBehaviour
    {
        [Serializable] public sealed class Finger
        {
            public string name;
            public Transform[] joints;
            public Quaternion[] restRotations;
            public Vector3 maximumCurl=new Vector3(60,85,65);
        }
        public Transform itemSocket;
        public Finger[] fingers;
        public HandItemProfile startingItem;
        [Min(0.01f)] public float gripTransitionSeconds=0.18f;
        public HandItemProfile CurrentItem {get;private set;}
        public GameObject EquippedInstance {get;private set;}
        readonly float[] curls=new float[5];
        float opposition;
        bool initialized;

        void Start() { if(!initialized) Equip(startingItem); }
        public void Equip(HandItemProfile profile)
        {
            if(!itemSocket)throw new InvalidOperationException("Assign a right-hand ItemSocket first.");
            if(EquippedInstance)
            {
                EquippedInstance.SetActive(false);
                if(Application.isPlaying)Destroy(EquippedInstance);else DestroyImmediate(EquippedInstance);
            }
            EquippedInstance=null;CurrentItem=profile;initialized=true;
            if(profile && profile.itemPrefab)
            {
                EquippedInstance=Instantiate(profile.itemPrefab,itemSocket,false);
                EquippedInstance.name=profile.itemPrefab.name+"_Held";
                EquippedInstance.transform.localPosition=profile.localPosition;
                EquippedInstance.transform.localRotation=Quaternion.Euler(profile.localEulerAngles);
                // Profile scale multiplies the source prefab's authored scale.
                EquippedInstance.transform.localScale=Vector3.Scale(profile.itemPrefab.transform.localScale,profile.localScale);
            }
        }
        public void Unequip() { Equip(null); }
        void LateUpdate() { ApplyPose(Time.deltaTime); }
        public void ApplyPose(float dt)
        {
            float[] target=CurrentItem?CurrentItem.Curls():new float[5];
            float blend=dt<0?1:1-Mathf.Exp(-dt*4/Mathf.Max(0.01f,gripTransitionSeconds));
            opposition=Mathf.Lerp(opposition,CurrentItem?CurrentItem.thumbOpposition:0,blend);
            for(int i=0;i<Mathf.Min(5,fingers.Length);i++)
            {
                curls[i]=Mathf.Lerp(curls[i],target[i],blend);
                var finger=fingers[i];
                for(int j=0;j<finger.joints.Length;j++)
                {
                    Quaternion extra=i==0 && j==0?Quaternion.AngleAxis(opposition,Vector3.up):Quaternion.identity;
                    finger.joints[j].localRotation=finger.restRotations[j]*extra*Quaternion.AngleAxis(finger.maximumCurl[j]*curls[i],Vector3.right);
                }
            }
        }
    }
}
