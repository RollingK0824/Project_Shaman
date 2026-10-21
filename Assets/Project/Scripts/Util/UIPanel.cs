using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] bool _unlocksCursor = true;
    public bool UnlocksCursor => _unlocksCursor;

    // 끄면 UIManager 없이 SetActive로만 열고 닫는다 (메인 메뉴, 관전 중 설정 패널)
    [SerializeField] bool _useUIManager = true;

    // 여는 버튼 OnClick에 연결. Close와 같은 이유로 패널을 거쳐 연다
    public void Open()
    {
        if (!_useUIManager)
        {
            gameObject.SetActive(true);
            return;
        }

        UIManager.Instance.Open(this);
    }

    // 닫기 버튼 OnClick에 연결. 프리팹은 씬의 UIManager를 직접 참조할 수 없어서 패널을 거쳐 닫는다
    public void Close()
    {
        if (!_useUIManager)
        {
            gameObject.SetActive(false);
            return;
        }

        UIManager.Instance.Close(this);
    }
}
