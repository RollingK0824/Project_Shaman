using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] bool _unlocksCursor = true;
    public bool UnlocksCursor => _unlocksCursor;

    // 닫기 버튼 OnClick에 연결. 프리팹은 씬의 UIManager를 직접 참조할 수 없어서 패널을 거쳐 닫는다
    public void Close()
    {
        UIManager.Instance.Close(this);
    }
}
