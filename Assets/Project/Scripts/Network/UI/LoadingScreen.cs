using UnityEngine;
using TMPro;
using Mirror;
using UnityEngine.UI;

// 로비 -> 게임 전환 동완 화면을 가리는 로딩 UI
// RoomManager의 자식이라 씬이 바뀌어도 유지.
// 표시/숨김만 담당하고, 언제 켜고 끌지는 RoomManager가 담당
public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject _panel;   // Panel Loading
    [SerializeField] private GameObject _loadingCamera;
    [SerializeField] private Image _progressFill;    // Progress Fill (Filled, Horizontal)
    [SerializeField] private TMP_Text _statusText;   // Text Status

    [Header("채우기 연출")]
    [SerializeField] private float _fillSpeed = 0.25f;      // 평소 초당 채우는 양 (0.25 = 4초에 가득)
    [SerializeField] private float _finishSpeed = 1.2f;     // 시작이 확정된 뒤 남은 구간을 채우는 속도
    [SerializeField] private float _stepPause = 0.35f;      // 단계 지점에 도착하면 멈추는 시간
    [SerializeField] private float _maxDeltaTime = 0.05f;   // 씬 활성화로 멈췄다 풀릴 때 한 번에 튀지 않도록
    [SerializeField] private float _completeHold = 0.3f;    // 100%를 보여 주는 시간

    private float _shownProgress;
    private float _pauseTimer;
    private float _lastCheckpoint;
    private bool _completing;
    private float _completeTimer;
    public bool IsVisible => _panel.activeSelf;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        _shownProgress = 0f;
        _pauseTimer = 0f;
        _lastCheckpoint = 0f;
        _completing = false;
        _progressFill.fillAmount = 0f;
        _statusText.text = "불러오는 중...";
        _panel.SetActive(true);
        if (_loadingCamera != null) _loadingCamera.SetActive(true);
    }

    public void Hide()
    {
        _completing = false;
        _panel.SetActive(false);
        if (_loadingCamera != null) _loadingCamera.SetActive(false);
    }

    public void Complete()
    {
        if (!_panel.activeSelf) return;

        _completing = true;
        _completeTimer = _completeHold;
        _statusText.text = "곧 시작합니다.";
    }

    private void Update()
    {
        if (!_panel.activeSelf) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, _maxDeltaTime);

        if (_completing)
        {
            Fill(1f, _finishSpeed, dt);
            if (_shownProgress >= 1f)
            {
                _completeTimer -= dt;
                if (_completeTimer <= 0f) Hide();
            }
            return;
        }


        float target = GetTargetProgress(out string status, out bool isCheckpoint);
        _statusText.text = status;

        // 단계 지점에서 잠깐 멈춤
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            return;
        }

        Fill(target, target >= 1f ? _finishSpeed : _fillSpeed, dt);

        // 단계 지점에 막 도착했으면 멈춤 (같은 지점에서 두 번 멈추지 않음)
        if (isCheckpoint && _shownProgress >= target && target > _lastCheckpoint)
        {
            _lastCheckpoint = target;
            _pauseTimer = _stepPause;
        }
    }

    // 뒤로는 가지 않고 목표까지 일정 속도로
    private void Fill(float target, float speed, float dt)
    {
        _shownProgress = Mathf.Max(_shownProgress, Mathf.MoveTowards(_shownProgress, target, speed * dt));
        _progressFill.fillAmount = _shownProgress;
    }

    // 1단계 씬 로딩(0~0.6) → 2단계 다른 플레이어 대기(0.6~0.95) → 3단계 시작 확정(1.0)
    private static float GetTargetProgress(out string status, out bool isCheckpoint)
    {
        AsyncOperation op = NetworkManager.loadingSceneAsync;
        if (op != null)
        {
            status = "불러오는 중...";
            isCheckpoint = false;
            return Mathf.Clamp01(op.progress / 0.9f) * 0.6f;
        }

        isCheckpoint = true;
        NetGameStartGate gate = NetGameStartGate.Instance;

        if (gate != null && gate.IsStartScheduled)
        {
            status = "곧 시작합니다";
            return 1f;
        }

        if (gate != null && gate.ExpectedCount > 0)
        {
            status = $"다른 무당을 기다리는 중... ({gate.ReadyCount}/{gate.ExpectedCount})";
            return 0.6f + 0.35f * gate.ReadyCount / gate.ExpectedCount;
        }

        status = "불러오는 중...";
        return 0.6f;
    }
}
