using UnityEngine;
namespace Exorcist.FirstPerson
{
    [CreateAssetMenu(menuName="Exorcist/Right Hand Item Profile")]
    public sealed class HandItemProfile : ScriptableObject
    {
        public GameObject itemPrefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale=Vector3.one;
        [Range(0,1)] public float thumb=0.6f;
        [Range(0,1)] public float index=0.8f;
        [Range(0,1)] public float middle=0.85f;
        [Range(0,1)] public float ring=0.9f;
        [Range(0,1)] public float pinky=0.9f;
        [Range(-60,90)] public float thumbOpposition=40;
        public float[] Curls() { return new[]{thumb,index,middle,ring,pinky}; }
    }
}
