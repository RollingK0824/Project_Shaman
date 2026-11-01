using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [로컬 클라] 근거리 음성 마이크 상태 HUD (화면 오른쪽 아래). 음소거 / 켜짐 / 말하는 중
// 관전자도 말할 수 있어서 죽어도 계속 보임. PlayerLocalUIBootstrap이 만들고 지움
public sealed class VoiceStatusHUD : MonoBehaviour
{
    public static readonly Color LiveColor = new(.96f, .96f, .94f, .55f);
    public static readonly Color SpeakingColor = new(.55f, .92f, .62f, 1f);
    public static readonly Color MutedColor = new(.92f, .28f, .22f, 1f);

    private enum State { None, Muted, Live, Speaking }

    private NetPlayerVoice _voice;
    private RectTransform _panel;
    private Image _icon;
    private TMP_Text _label;
    private State _state;
    private string _keyHint;
    private float _nextKeyCheck;

    public static VoiceStatusHUD Create(NetPlayerVoice voice)
    {
        var root = new GameObject("Local Voice HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        var hud = root.AddComponent<VoiceStatusHUD>();
        hud._voice = voice;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        var art = PlayerHUDArt.Current;

        _panel = new GameObject("Mic Status", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        _panel.SetParent(transform, false);
        PlayerItemUIStyle.Place(_panel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(150, 40));
        var background = _panel.GetComponent<Image>();
        background.sprite = art != null ? art.InkWash : null;
        background.color = art != null ? new Color(1, 1, 1, .68f) : new Color(.06f, .07f, .08f, .76f);
        background.raycastTarget = false;

        _icon = new GameObject("Mic Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        _icon.rectTransform.SetParent(_panel, false);
        PlayerItemUIStyle.Place(_icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(28, 28));
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        _label = new GameObject("Mic Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        _label.rectTransform.SetParent(_panel, false);
        PlayerItemUIStyle.Stretch(_label.rectTransform, new Vector2(44, 2), new Vector2(-10, -2));
        PlayerItemUIStyle.Label(_label, art != null ? art.Font : null, 16);
        _label.richText = false;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void LateUpdate()
    {
        if (_voice == null)
        {
            _panel.gameObject.SetActive(false);
            return;
        }

        // 키 재설정은 설정 창에서 하니 가끔만 다시 읽음
        if (Time.unscaledTime >= _nextKeyCheck)
        {
            _nextKeyCheck = Time.unscaledTime + 1f;
            string hint = ReadKeyHint();
            if (hint != _keyHint)
            {
                _keyHint = hint;
                _state = State.None;
            }
        }

        State state = VoiceSettings.MicMuted ? State.Muted : _voice.IsSpeaking ? State.Speaking : State.Live;
        if (state == _state) return;
        _state = state;

        bool muted = state == State.Muted;
        _icon.sprite = MicIconSprite.Get(muted);
        _icon.color = muted ? MutedColor : state == State.Speaking ? SpeakingColor : LiveColor;
        string text = muted ? "마이크 꺼짐" : "마이크 켜짐";
        _label.text = string.IsNullOrEmpty(_keyHint) ? text : $"{text} [{_keyHint}]";
        _label.color = muted ? MutedColor : PlayerHUDArt.Text;
    }

    private static string ReadKeyHint()
    {
        var action = InputManager.Instance != null ? InputManager.Instance.Actions.asset.FindAction("UI/MicMute", false) : null;
        return action != null ? action.GetBindingDisplayString() : null;
    }
}
