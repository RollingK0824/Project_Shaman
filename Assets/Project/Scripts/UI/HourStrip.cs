using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 우상단 시 띠. 시간이 흐르는 대로 왼쪽으로 흐르고, 고정된 '지금' 표시 아래로 칸이 지나간다 (CompassBar와 같은 방식)
public class HourStrip : MonoBehaviour
{
    private const int HOUR_COUNT = 12;

    // 폭 0 RectTransform, 앵커·피벗 가운데, RectMask2D 뷰포트 안. 뷰포트 가운데 = 지금. 칸은 이 아래에 생성된다
    [SerializeField]
    private RectTransform _strip;

    // _strip 안의 비활성 템플릿, 앵커·피벗 가운데. y는 템플릿 위치를 그대로 쓴다. _hourTemplate·_dividerTemplate은 선택
    [SerializeField]
    private TMP_Text _nameTemplate;

    [SerializeField]
    private TMP_Text _hourTemplate;

    [SerializeField]
    private Image _dividerTemplate;

    [SerializeField]
    private float _cellWidth = 60f;

    // 자시부터 (TimeManager._textTime과 같은 순서)
    [SerializeField]
    private string[] _names = { "자", "축", "인", "묘", "진", "사", "오", "미", "신", "유", "술", "해" };

    private void Awake()
    {
        BuildStrip();
    }

    private void LateUpdate()
    {
        TimeManager time = TimeManager.Instance;
        if (time == null || _strip == null)
        {
            return;
        }

        _strip.anchoredPosition = new Vector2(-time.TextTimePosition * _cellWidth, _strip.anchoredPosition.y);
    }

    // -12 ~ 24칸(세 바퀴)을 깔아 0 ~ 12 어느 위치에서도 양옆이 다 차 있다 (감싸기 처리 없음)
    // 칸 i는 i ~ i+1 사이. 칸 앞 경계에 그 시가 시작하는 시각(자시 = 23시)을 단다
    private void BuildStrip()
    {
        if (_strip == null || _nameTemplate == null || _names.Length != HOUR_COUNT)
        {
            return;
        }

        for (int i = -HOUR_COUNT; i < HOUR_COUNT * 2; i++)
        {
            int index = (i % HOUR_COUNT + HOUR_COUNT) % HOUR_COUNT;

            TMP_Text name = Instantiate(_nameTemplate, _strip);
            name.text = _names[index];
            Place(name.rectTransform, i + 0.5f);

            if (_hourTemplate != null)
            {
                TMP_Text hour = Instantiate(_hourTemplate, _strip);
                hour.text = ((23 + index * 2) % 24).ToString();
                Place(hour.rectTransform, i);
            }

            if (_dividerTemplate != null)
            {
                Image divider = Instantiate(_dividerTemplate, _strip);
                Place(divider.rectTransform, i);
            }
        }
    }

    private void Place(RectTransform item, float cell)
    {
        item.gameObject.SetActive(true);
        item.anchoredPosition = new Vector2(cell * _cellWidth, item.anchoredPosition.y);
    }
}
