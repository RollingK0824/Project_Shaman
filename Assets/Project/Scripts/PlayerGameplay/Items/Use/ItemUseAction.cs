using UnityEngine;

// 아이템 사용 판정의 기본 클래스..
// ItemData가 참조하기에 서버는 비주얼 없이 ID만으로 실행 가능
public abstract class ItemUseAction : ScriptableObject
{
    [SerializeField, Min(0f)] private float _cooldown;
    public float Cooldown => _cooldown;

    // [판정자 전용]
    public abstract bool Execute(in ItemUseContext context);

    // [연출 전용]
    public virtual void PlayRemotePresentation(in ItemUseContext context) { }

    // 연출용 이펙트 프리팹을 잠깐 띄우는 공용 함수
    protected static void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null) return;

        Destroy(Instantiate(prefab, position, rotation), Mathf.Max(.1f, lifetime));
    }
}
