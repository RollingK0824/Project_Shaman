using UnityEngine;

// [로컬] 메인 메뉴 크레딧. 열면 아래에서 위로 자동 스크롤, 끝까지 가거나 Esc/닫기 버튼이면 닫힘
public class CreditUI : MonoBehaviour
{
    [SerializeField] private RectTransform _viewport;   // 보이는 영역 (RectMask2D)
    [SerializeField] private RectTransform _content;    // 올라갈 내용 (크레딧 TMP 텍스트)
    [SerializeField] private GameObject _mainMenu;      // 크레딧 동안 숨길 Panel MainMenuUI
    [SerializeField, Min(1f)] private float _scrollSpeed = 60f;  // 초당 픽셀

    private float _endY;

    // Button Credit OnClick
    public void Open()
    {
        gameObject.SetActive(true);
        if (_mainMenu != null) _mainMenu.SetActive(false);

        // ContentSizeFitter가 정한 텍스트 높이를 이번 프레임에 바로 읽기 위해
        Canvas.ForceUpdateCanvases();

        // 내용의 윗변을 보이는 영역 아래 끝에서 시작 -> 아랫변이 위로 다 빠져나가면 끝
        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, -_viewport.rect.height);
        _endY = _content.rect.height;
    }

    // 닫기 버튼 OnClick
    public void Close()
    {
        gameObject.SetActive(false);
        if (_mainMenu != null) _mainMenu.SetActive(true);
    }

    private void Update()
    {
        if (InputManager.Instance.Actions.UI.Cancel.WasPerformedThisFrame())
        {
            Close();
            return;
        }

        Vector2 pos = _content.anchoredPosition;
        pos.y += _scrollSpeed * Time.unscaledDeltaTime;
        _content.anchoredPosition = pos;

        if (pos.y >= _endY) Close();
    }
}