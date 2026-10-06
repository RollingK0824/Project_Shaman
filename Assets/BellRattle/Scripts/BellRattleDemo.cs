using UnityEngine;
namespace OrnateBellRattle
{
    public sealed class BellRattleDemo : MonoBehaviour
    {
        public bool shake = true;
        [Range(0, 0.1f)] public float distance = 0.028f;
        [Range(0.1f, 8)] public float frequency = 3;
        Vector3 origin;
        Quaternion rotation;
        float clock;
        void OnEnable() { origin = transform.localPosition; rotation = transform.localRotation; clock = 0; }
        void Update()
        {
            clock += Time.deltaTime;
            float cycle = clock % 5;
            float envelope = shake && cycle < 2 ? Mathf.Sin(Mathf.PI * cycle / 2) : 0;
            float wave = Mathf.Sin(clock * frequency * 2 * Mathf.PI) * envelope;
            transform.localPosition = origin + new Vector3(wave * distance, 0, 0);
            transform.localRotation = rotation * Quaternion.Euler(0, wave * 9, wave * 7);
        }
        void OnDisable() { transform.localPosition = origin; transform.localRotation = rotation; }
        void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 310, 116), "Ornate Bell Rattle - realtime demo");
            shake = GUI.Toggle(new Rect(35, 52, 270, 25), shake, "Shake (2 seconds), settle (3 seconds)");
            GUI.Label(new Rect(35, 82, 270, 25), "12 independent bells / spatial chimes");
        }
    }
}
