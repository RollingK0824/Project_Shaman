using UnityEngine;
using TMPro;
using UnityEngine.UI;

// [호스트, 클라] 디펜스 진행 표시. NetDefenseState만 읽어오기
public class DefenseHUD : MonoBehaviour
{
    [Header("알림 배너 (단계가 바뀔 때 잠깐)")]
    [SerializeField] private GameObject _bannerRoot;
    [SerializeField] private TMP_Text _bannerText;
    [SerializeField, Min(0.5f)] private float _bannerDuration = 4f;

    [Header("진행 패널 (의식 중 계속)")]
    [SerializeField] private GameObject _statusRoot;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Image _hpFill;

    private NetDefenseState _state;
    private float _bannerHideTime;

    // 단계상 진행 패널을 보여야 하는데, 신제 표시는 배너가 없을 때만
    private bool _wantStatus;

    private void OnEnable()
    {
        _bannerRoot.SetActive(false);
        _statusRoot.SetActive(false);
        TrySubscribe();
    }

    private void OnDisable()
    {
        if(_state != null)
        {
            _state.PhaseChanged -= HandlePhaseChanged;
            _state = null;
        }
    }

    // 씬 오브젝트 Awake 순서에 따라 NetDefenseState.Instance가 아직 없을 수도 있어서 Update에서도 한번 더 시도
    private void TrySubscribe()
    {
        _state = NetDefenseState.Instance;
        if (_state == null) return;

        _state.PhaseChanged += HandlePhaseChanged;

        // 중간에 켜졌거나 늦게 접속 : 알림 없이 현재 단계만 표시
        ShowStatus(_state.Phase);
    }

    private void Update()
    {
        if (_state == null)
        {
            TrySubscribe();
            return;
        }

        if (_bannerRoot.activeSelf && Time.time >= _bannerHideTime)
        {
            _bannerRoot.SetActive(false);
            RefreshStatusVisible();
        }

        if(_statusRoot.activeSelf)
        {
            _timerText.text = FormatTime(_state.RemainingTime);

            // 디펜스 전(깃발 대기, 준비 시간)에는 깃발 HP가 아직 초기화 전(지난밤 값)이라 가득 찬 것으로 표시
            bool full = _state.Phase != RitualPhase.Active || _state.FlagMaxHP <= 0f;
            _hpFill.fillAmount = full ? 1f : _state.FlagHP / _state.FlagMaxHP;
        }
    }

    private void HandlePhaseChanged(RitualPhase oldPhase, RitualPhase newPhase)
    {
        ShowStatus(newPhase);

        string message = GetBannerMessage(newPhase);
        if (message != null)
        {
            ShowBanner(message);
        }
    }

    private void ShowStatus(RitualPhase phase)
    {
        _wantStatus = phase == RitualPhase.WaitingFlag || phase == RitualPhase.Countdown || phase == RitualPhase.Active;
        RefreshStatusVisible();
    }

    private void RefreshStatusVisible()
    {
        _statusRoot.SetActive(_wantStatus && !_bannerRoot.activeSelf);
    }

    private static string GetBannerMessage(RitualPhase phase) => phase switch
    {
        RitualPhase.NoRitual => "오늘 밤은 굿을 올릴 수 없습니다.",
        RitualPhase.WaitingFlag => "원귀의 기운이 느껴집니다. 사당의 깃발을 세워주세요.",
        RitualPhase.Countdown => "굿이 시작되었습니다. 잡귀가 몰려옵니다.",
        RitualPhase.Succeeded => "퇴마 성공!",
        RitualPhase.Failed => "퇴마 실패...",
        RitualPhase.FlagMissed => "깃발을 세우지 못해 굿을 올리지 못했습니다.",
        _ => null
    };

    private void ShowBanner(string message)
    {
        _bannerText.text = message;
        _bannerRoot.SetActive(true);
        _bannerHideTime = Time.time + _bannerDuration;

        RefreshStatusVisible();
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }
}
