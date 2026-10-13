using UnityEngine;


// 로비 -> 게임 전환 동완 화면을 가리는 로딩 UI
// RoomManager의 자식이라 씬이 바뀌어도 유지.
// 표시/숨김만 담당하고, 언제 켜고 끌지는 RoomManager가 담당
public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject _panel;   // Panel Loading

    public bool IsVisible => _panel.activeSelf;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        _panel.SetActive(true);
    }

    public void Hide()
    {
        _panel.SetActive(false);
    }
}
