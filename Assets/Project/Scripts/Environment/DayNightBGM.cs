using UnityEngine;
using UnityEngine.Audio;

// [로컬] 낮/밤 BGM. TimeManager의 낮·밤 전환에 맞춰 두 곡을 크로스페이드
// 볼륨이 설정 메뉴(BGM)를 따르도록 MainMixer의 BGM 그룹으로 출력
// 시간 배속(F1)이 Time.timeScale을 바꾸므로 페이드는 unscaled 시간으로 진행
public class DayNightBGM : MonoBehaviour
{
    [Tooltip("MainMixer의 BGM 그룹")]
    [SerializeField] private AudioMixerGroup _bgmGroup;

    [Header("클립")]
    [SerializeField] private AudioClip _dayClip;
    [SerializeField] private AudioClip _nightClip;

    [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;
    [Tooltip("낮↔밤 전환 크로스페이드 시간 (초)")]
    [SerializeField, Min(0f)] private float _crossfadeTime = 4f;

    private TimeManager _time;
    private AudioSource _daySource;
    private AudioSource _nightSource;
    private AudioSource _target;

    private void Awake()
    {
        _daySource = CreateSource(_dayClip);
        _nightSource = CreateSource(_nightClip);
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
        _target = phase == TimeOfDay.Day ? _daySource : _nightSource;
        if (_target.clip != null && !_target.isPlaying)
        {
            _target.Play();
        }
    }

    private void Update()
    {
        if (_target == null)
        {
            return;
        }

        float step = _crossfadeTime > 0f ? _volume * Time.unscaledDeltaTime / _crossfadeTime : 1f;
        Fade(_daySource, _target == _daySource ? _volume : 0f, step);
        Fade(_nightSource, _target == _nightSource ? _volume : 0f, step);
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
