using UnityEngine;
using UnityEngine.Audio;

// [로컬] 낮/밤 BGM. TimeManager의 낮·밤 전환에 맞춰 두 곡을 크로스페이드
// 디펜스(굿) 중에는 디펜스 곡으로 바꾸고, 끝나면 그때의 낮·밤 곡으로 돌아감 (NetDefenseState 단계를 모든 클라가 봄)
// 볼륨이 설정 메뉴(BGM)를 따르도록 MainMixer의 BGM 그룹으로 출력
// 시간 배속(F1)이 Time.timeScale을 바꾸므로 페이드는 unscaled 시간으로 진행
public class DayNightBGM : MonoBehaviour
{
    [Tooltip("MainMixer의 BGM 그룹")]
    [SerializeField] private AudioMixerGroup _bgmGroup;

    [Header("클립")]
    [SerializeField] private AudioClip _dayClip;
    [SerializeField] private AudioClip _nightClip;
    [Tooltip("디펜스(굿) 중 재생")]
    [SerializeField] private AudioClip _defenseClip;

    [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;
    [Tooltip("낮↔밤 전환 크로스페이드 시간 (초)")]
    [SerializeField, Min(0f)] private float _crossfadeTime = 4f;

    private TimeManager _time;
    private AudioSource _daySource;
    private AudioSource _nightSource;
    private AudioSource _defenseSource;
    private AudioSource _target;
    private TimeOfDay _phase = TimeOfDay.Day;
    private bool _isDefense;
    // 씬 오브젝트라 늦게 생길 수 있어 Update에서 찾아 구독
    private NetDefenseState _defense;

    private void Awake()
    {
        _daySource = CreateSource(_dayClip);
        _nightSource = CreateSource(_nightClip);
        _defenseSource = CreateSource(_defenseClip);
    }

    private AudioSource CreateSource(AudioClip clip)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.clip = clip;
        source.outputAudioMixerGroup = _bgmGroup;
        return source;
    }

    private void OnEnable()
    {
        _time = TimeManager.Instance;
        if (_time == null)
        {
            return;
        }

        _time.OnDayStart += HandleDayStart;
        _time.OnNightStart += HandleNightStart;
    }

    private void OnDisable()
    {
        if (_time != null)
        {
            _time.OnDayStart -= HandleDayStart;
            _time.OnNightStart -= HandleNightStart;
        }

        if (_defense != null)
        {
            _defense.PhaseChanged -= HandleDefensePhaseChanged;
            _defense = null;
        }
    }

    private void WatchDefense()
    {
        if (_defense != null || NetDefenseState.Instance == null)
        {
            return;
        }

        _defense = NetDefenseState.Instance;
        _defense.PhaseChanged += HandleDefensePhaseChanged;
        HandleDefensePhaseChanged(RitualPhase.None, _defense.Phase);
    }

    private void HandleDefensePhaseChanged(RitualPhase oldPhase, RitualPhase newPhase)
    {
        bool isDefense = newPhase == RitualPhase.Active;
        if (isDefense == _isDefense)
        {
            return;
        }

        _isDefense = isDefense;
        SetPhase(_phase);
    }

    private void Start()
    {
        // 구독 전에 TimeManager가 이미 첫 단계를 알린 경우 현재 단계로 맞춤
        if (_target == null && _time != null && _time.IsRunning && _time.Elapsed >= 0)
        {
            SetPhase(_time.CurrentTimePhase);
        }
    }

    private void HandleDayStart() => SetPhase(TimeOfDay.Day);
    private void HandleNightStart() => SetPhase(TimeOfDay.Night);

    private void SetPhase(TimeOfDay phase)
    {
        _phase = phase;
        _target = _isDefense && _defenseSource.clip != null ? _defenseSource : phase == TimeOfDay.Day ? _daySource : _nightSource;
        if (_target.clip != null && !_target.isPlaying)
        {
            _target.Play();
        }
    }

    private void Update()
    {
        WatchDefense();

        if (_target == null)
        {
            return;
        }

        float step = _crossfadeTime > 0f ? _volume * Time.unscaledDeltaTime / _crossfadeTime : 1f;
        Fade(_daySource, _target == _daySource ? _volume : 0f, step);
        Fade(_nightSource, _target == _nightSource ? _volume : 0f, step);
        Fade(_defenseSource, _target == _defenseSource ? _volume : 0f, step);
    }

    private static void Fade(AudioSource source, float targetVolume, float step)
    {
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, step);

        // 다 꺼진 곡은 멈춰서 다음 단계에 처음부터 다시 시작
        if (source.volume <= 0f && targetVolume <= 0f && source.isPlaying)
        {
            source.Stop();
        }
    }
}
