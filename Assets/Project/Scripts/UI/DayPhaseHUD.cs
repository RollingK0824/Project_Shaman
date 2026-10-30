using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 우상단 일차·낮밤 글자와 12지 아이콘. 시 띠는 HourStrip
// 글자: 일차나 낮밤이 바뀌면 숨김 → 교체 → 오른쪽부터 등장
// 아이콘: 시가 바뀌면 화면 위로 빠짐 → 스프라이트 교체 → 내려옴
public class DayPhaseHUD : MonoBehaviour
{
    private const int ZODIAC_COUNT = 12;

    [SerializeField]
    private TMP_Text _displayText;

    // 글자 와이프용. 왼쪽 padding을 폭→0으로 줄이면 오른쪽부터 드러난다 (Softness X를 주면 페이드처럼 보임)
    [SerializeField]
    private RectMask2D _textMask;

    [Header("Zodiac Icon")]
    // 씬에 놓인 위치가 보이는 위치
    [SerializeField]
    private Image _icon;

    // 자시 → 해시 순서 (TimeManager._textTime과 같은 순서)
    [SerializeField]
    private Sprite[] _zodiacSprites = new Sprite[ZODIAC_COUNT];

    // 위로 빠지는 거리. 아이콘이 화면 위 가장자리 밖으로 완전히 나갈 만큼
    [SerializeField]
    private float _hideDistance = 150f;

    [Header("Timing")]
    [SerializeField]
    private float _wipeDuration = 0.3f;

    [SerializeField]
    private float _iconDuration = 0.35f;

    private TimeManager _time;
    private Sequence _textSeq;
    private Sequence _iconSeq;
    private float _iconShownY;
    private string _targetText;
    private int _shownIndex = -1;
    private bool _textDirty;
    private bool _timeChanged;

    private bool HasIcon => _icon != null && _zodiacSprites.Length == ZODIAC_COUNT;

    private void Awake()
    {
        if (_icon != null)
        {
            _iconShownY = _icon.rectTransform.anchoredPosition.y;
        }
    }

    private void OnEnable()
    {
        _time = TimeManager.Instance;
        if (_time == null)
        {
            return;
        }

        _time.OnNewDay += HandleNewDay;
        _time.OnDayStart += HandlePhaseChange;
        _time.OnNightStart += HandlePhaseChange;
        _time.OnTextTimeChange += HandleTimeChange;
        Snap();
    }

    private void OnDisable()
    {
        if (_time != null)
        {
            _time.OnNewDay -= HandleNewDay;
            _time.OnDayStart -= HandlePhaseChange;
            _time.OnNightStart -= HandlePhaseChange;
            _time.OnTextTimeChange -= HandleTimeChange;
        }
        _time = null;

        Finish(ref _textSeq);
        Finish(ref _iconSeq);
    }

    private void HandleNewDay(int day) => _textDirty = true;

    private void HandlePhaseChange() => _textDirty = true;

    private void HandleTimeChange() => _timeChanged = true;

    // 새 날 + 낮 + 시가 한 프레임에 같이 온다. TimeManager.Update 뒤에 한 번만 처리
    private void LateUpdate()
    {
        if (_time == null)
        {
            return;
        }

        if (_textDirty)
        {
            _textDirty = false;
            string text = BuildText();
            if (text != _targetText)
            {
                PlayText(text);
            }
        }

        if (_timeChanged)
        {
            _timeChanged = false;
            int index = _time.TextTimeIndex;
            int steps = (index - _shownIndex + ZODIAC_COUNT) % ZODIAC_COUNT;
            if (steps == 1)
            {
                PlayIcon(index);
            }
            else if (steps != 0)
            {
                // 배속으로 시를 건너뛰면 애니메이션 없이 맞춘다
                Finish(ref _iconSeq);
                SnapIcon(index);
            }
        }
    }

    private void PlayText(string text)
    {
        Finish(ref _textSeq);
        _targetText = text;

        if (_displayText == null)
        {
            return;
        }

        if (_textMask == null)
        {
            _displayText.text = text;
            return;
        }

        _textSeq = DOTween.Sequence().SetLink(gameObject);
        _textSeq.Append(TextWipe(false));
        _textSeq.AppendCallback(() => _displayText.text = text);
        _textSeq.Append(TextWipe(true));
    }

    private void PlayIcon(int index)
    {
        Finish(ref _iconSeq);
        if (!HasIcon)
        {
            return;
        }

        _shownIndex = index;
        _iconSeq = DOTween.Sequence().SetLink(gameObject);
        _iconSeq.Append(MoveIcon(_iconShownY + _hideDistance).SetEase(Ease.InCubic));
        _iconSeq.AppendCallback(() => _icon.sprite = _zodiacSprites[index]);
        _iconSeq.Append(MoveIcon(_iconShownY).SetEase(Ease.OutCubic));
    }

    private Sequence TextWipe(bool show)
    {
        float width = _textMask.rectTransform.rect.width;
        Ease ease = show ? Ease.OutCubic : Ease.InCubic;

        return DOTween.Sequence()
            .Join(DOTween.To(() => _textMask.padding.x, SetMaskLeft, show ? 0f : width, _wipeDuration).SetEase(ease))
            .Join(DOTween.To(() => _displayText.alpha, a => _displayText.alpha = a, show ? 1f : 0f, _wipeDuration).SetEase(ease));
    }

    // 모듈(DOAnchorPosY) 대신 코어 DOTween.To만 써서 DOTween.Modules 참조 없이도 컴파일된다
    private Tween MoveIcon(float y)
    {
        RectTransform rect = _icon.rectTransform;
        return DOTween.To(() => rect.anchoredPosition.y,
            value => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, value),
            y, _iconDuration);
    }

    private void SetMaskLeft(float left)
    {
        Vector4 padding = _textMask.padding;
        padding.x = left;
        _textMask.padding = padding;
    }

    // 콜백까지 실행해서 끝내야 글자·스프라이트가 목표 상태로 맞는다
    private static void Finish(ref Sequence seq)
    {
        if (seq == null)
        {
            return;
        }

        seq.Complete(true);
        seq.Kill();
        seq = null;
    }

    // 애니메이션 없이 현재 시간에 맞춘다
    private void Snap()
    {
        _textDirty = false;
        _timeChanged = false;
        _targetText = BuildText();

        if (_displayText != null)
        {
            _displayText.text = _targetText;
            _displayText.alpha = 1f;
        }
        if (_textMask != null)
        {
            SetMaskLeft(0f);
        }

        SnapIcon(_time.TextTimeIndex);
    }

    private void SnapIcon(int index)
    {
        if (!HasIcon)
        {
            return;
        }

        _shownIndex = index;
        _icon.sprite = _zodiacSprites[index];
        RectTransform rect = _icon.rectTransform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, _iconShownY);
    }

    private string BuildText()
    {
        int day = Mathf.Max(1, _time.DayCount);
        string phase = _time.CurrentTimePhase == TimeOfDay.Day ? "낮" : "밤";
        return $"{day} 일차 - {phase}";
    }
}
