using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] bool _unlocksCursor = true;
    public bool UnlocksCursor => _unlocksCursor;

    // 여는 버튼 OnClick에 연결. Close와 같은 이유로 패널을 거쳐 연다
    public void Open()
    {
        UIManager.Instance.Open(this);
    }

    // 닫기 버튼 OnClick에 연결. 프리팹은 씬의 UIManager를 직접 참조할 수 없어서 패널을 거쳐 닫는다
    public void Close()
    {
        UIManager.Instance.Close(this);
    }
}
