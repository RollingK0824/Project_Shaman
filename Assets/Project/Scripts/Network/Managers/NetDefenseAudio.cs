using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// [로컬 클라] 디펜스(굿) 단계별 사운드. NetDefenseState와 같은 오브젝트에 붙임
// 단계는 NetDefenseState의 SyncVar(Phase)로 이미 동기화되므로 각 클라가 받아서 재생만 함
// - Countdown : 징 (2D, 맵 전체)
// - Active    : 굿 음악 반복 (깃발 위치 3D)
// - 깃발 피격 : 타격음 (깃발 위치 3D)
// - Succeeded : 방울 + 음악 페이드아웃 / Failed : 낮은 징 + 음악 정지
public class NetDefenseAudio : MonoBehaviour
{
    [SerializeField] private NetDefenseState _state;
    [Tooltip("굿 음악·피격음이 나는 위치 (ExorcismFlag)")]
    [SerializeField] private Transform _flagPoint;
    [Tooltip("굿 음악 볼륨이 설정 메뉴(SFX)를 따르도록 MainMixer의 SFX 그룹 지정")]
    [SerializeField] private AudioMixerGroup _sfxGroup;

    [Header("클립")]
    [SerializeField] private AudioClip _startClip;    // 깃발 켬 (징)
    [SerializeField] private AudioClip _loopClip;     // 디펜스 중 굿 음악 (반복)
    [SerializeField] private AudioClip _flagHitClip;  // 깃발 피격
    [SerializeField] private AudioClip _successClip;  // 디펜스 성공 (방울)
    [SerializeField] private AudioClip _failClip;     // 디펜스 실패 (낮은 징)

    [Header("굿 음악")]
    [SerializeField, Range(0f, 1f)] private float _loopVolume = 0.8f;
    [SerializeField, Min(1f)] private float _loopMinDistance = 5f;
    [SerializeField, Min(1f)] private float _loopMaxDistance = 40f;
    [SerializeField, Min(0f)] private float _fadeOutTime = 2f;

    [Header("피격음")]
    [Tooltip("잡귀 여러 마리가 동시에 때려도 이 간격보다 자주 재생하지 않음")]
    [SerializeField, Min(0f)] private float _hitInterval = 0.3f;

    private AudioSource _loopSource;
    private Coroutine _fadeRoutine;
    private float _lastFlagHp = -1f;
    private float _nextHitTime;

    private void Reset()
    {
        _state = GetComponent<NetDefenseState>();
    }

    private void Awake()
    {
        if (_state == null) _state = GetComponent<NetDefenseState>();

        Transform anchor = _flagPoint != null ? _flagPoint : transform;
        GameObject loopObj = new GameObject("DefenseLoopAudio");
        loopObj.transform.SetParent(anchor, false);

        _loopSource = loopObj.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.spatialBlend = 1f;
        _loopSource.rolloffMode = AudioRolloffMode.Linear;
        _loopSource.minDistance = _loopMinDistance;
        _loopSource.maxDistance = _loopMaxDistance;
        _loopSource.outputAudioMixerGroup = _sfxGroup;
    }

    private void OnEnable()
    {
        if (_state != null) _state.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (_state != null) _state.PhaseChanged -= HandlePhaseChanged;
    }

    private void Start()
    {
        // 디펜스 도중에 씬이 열린 경우(늦은 접속) hook 없이 현재 단계만 들어오므로 음악만 맞춰 줌
        if (_state != null && _state.Phase == RitualPhase.Active) StartLoop();
    }

    private void HandlePhaseChanged(RitualPhase oldPhase, RitualPhase newPhase)
    {
        switch (newPhase)
        {
            case RitualPhase.Countdown:
                Play2D(_startClip);
                _lastFlagHp = -1f;
                break;

            case RitualPhase.Active:
                StartLoop();
                break;

            case RitualPhase.Succeeded:
                Play2D(_successClip);
                FadeOutLoop();
                break;

            case RitualPhase.Failed:
                Play2D(_failClip);
                StopLoop();
                break;

            default:
                StopLoop();
                break;
        }
    }

    private void Update()
    {
        if (_state == null) return;

        // 디펜스 중 전멸 등으로 게임이 끝나면 단계가 Active로 남아 있어도 음악 정지
        GameManager gm = GameManager.Existing;
        if (gm != null && gm.IsGameOver && _loopSource.isPlaying && _fadeRoutine == null)
        {
            StopLoop();
        }

        CheckFlagHit();
    }

    // 깃발 HP는 SyncVar(hook 없음)라 값이 줄어든 순간을 직접 감지
    private void CheckFlagHit()
    {
        RitualPhase phase = _state.Phase;
        if (phase != RitualPhase.Countdown && phase != RitualPhase.Active)
        {
            _lastFlagHp = -1f;
            return;
        }

        float hp = _state.FlagHP;
        bool damaged = _lastFlagHp >= 0f && hp < _lastFlagHp;
        _lastFlagHp = hp;

        if (!damaged || _flagHitClip == null || Time.unscaledTime < _nextHitTime) return;

        _nextHitTime = Time.unscaledTime + _hitInterval;
        Vector3 pos = _flagPoint != null ? _flagPoint.position : transform.position;
        SoundManager.Instance.PlaySFXAt(_flagHitClip, pos);
    }

    private static void Play2D(AudioClip clip)
    {
        if (clip == null) return;
        SoundManager.Instance.PlaySFX(clip);
    }

    private void StartLoop()
    {
        if (_loopClip == null) return;

        CancelFade();
        _loopSource.clip = _loopClip;
        _loopSource.volume = _loopVolume;
        if (!_loopSource.isPlaying) _loopSource.Play();
    }

    private void StopLoop()
    {
        CancelFade();
        _loopSource.Stop();
    }

    private void FadeOutLoop()
    {
        if (!_loopSource.isPlaying) return;

        CancelFade();
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private void CancelFade()
    {
        if (_fadeRoutine == null) return;
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    // 성공 직후 결과 화면이 뜨면 timeScale이 0이 되므로 unscaled 시간으로 페이드
    private IEnumerator FadeOutRoutine()
    {
        float start = _loopSource.volume;
        float t = 0f;
        while (t < _fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            _loopSource.volume = Mathf.Lerp(start, 0f, t / _fadeOutTime);
            yield return null;
        }

        _loopSource.Stop();
        _loopSource.volume = _loopVolume;
        _fadeRoutine = null;
    }
}
