using UnityEngine;


// [모든 클라] 받은 음성을 링 버퍼에 쌓아 두고 스트리밍 AudioClip으로 꺼내서 재생
// 근거리용(3D)과 무전용(2D + 필터) 오브젝트에 하나씩 붙임

[RequireComponent(typeof(AudioSource))]
public class VoicePlayback : MonoBehaviour
{
    // 스트리밍 클립은 한 번에 4096샘플(약 85ms)씩 꺼내 가서, 이보다 넉넉해야 네트워크 흔들림에 끊기지 않음
    [SerializeField, Min(.02f)] private float _startDelay = .2f;
    [SerializeField, Min(.2f)] private float _bufferSeconds = 1f;
    // AudioSource.volume은 1이 상한이라 샘플에 직접 곱함. 큰 소리는 tanh로 부드럽게 눌러 찢어짐 방지
    [SerializeField, Range(1f, 8f)] private float _gain = 3f;

    private readonly object _lock = new object();
    private AudioSource _source;
    private float[] _ring;
    private int _readIndex;
    private int _writeIndex;
    private int _count;
    private int _startSamples;
    private bool _buffering = true;

    // 플레이어별 볼륨 (0~1). 전체 음성 볼륨에 곱해서 적용
    private float _playerVolume = 1f;

    public bool IsInitialized => _ring != null;

    public void Init(int sampleRate)
    {
        if (IsInitialized) return;

        _source = GetComponent<AudioSource>();
        ApplyVolume(VoiceSettings.Volume);
        VoiceSettings.VolumeChanged += ApplyVolume;
        _ring = new float[Mathf.CeilToInt(sampleRate * _bufferSeconds)];
        _startSamples = Mathf.CeilToInt(sampleRate * _startDelay);

        // 동시에 실제로 재생되는 소리는 32개(Real Voices)까지. 넘치면 우선순위 낮은 것부터 가상화돼 끊기므로 음성을 최우선으로
        _source.priority = 0;
        _source.clip = AudioClip.Create($"Voice_{transform.root.name}_{name}", sampleRate, 1, sampleRate, true, OnAudioRead);
        _source.loop = true;
        _source.Play();
    }

    public void SetSpatial(bool spatial)
    {
        if (_source != null) _source.spatialBlend = spatial ? 1f : 0f;
    }
    public void SetPlayerVolume(float volume)
    {
        _playerVolume = Mathf.Clamp01(volume);
        ApplyVolume(VoiceSettings.Volume);
    }

    // Init 전에는 _source가 없음. 값만 저장해 두고 Init에서 적용
    private void ApplyVolume(float volume)
    {
        if (_source != null) _source.volume = volume * _playerVolume;
    }

    // 무전 신호음. 같은 AudioSource로 재생해서 필터와 음성 볼륨이 그대로 적용
    public void PlayCue(AudioClip clip, float volumeScale)
    {
        if (_source != null && clip != null) _source.PlayOneShot(clip, volumeScale);
    }

    // [메인 스레드] Opus 디코딩 결과 (모노, -1~1)
    public void Push(float[] samples, int count)
    {
        if (!IsInitialized) return;

        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                // 가득 차면 가장 오래된 샘플을 버림 (지연이 계속 쌓이는 것 방지){
                if (_count == _ring.Length)
                {
                    _readIndex = (_readIndex + 1) % _ring.Length;
                    _count--;
                }

                _ring[_writeIndex] = samples[i];
                _writeIndex = (_writeIndex + 1) % _ring.Length;
                _count++;
            }
        }
    }

    // [오디오 스레드일 수 있음] 스트리밍 클립이 다음 데이터를 요구할 때 호출
    private void OnAudioRead(float[] data)
    {
        lock (_lock)
        {
            if (_buffering && _count >= _startSamples) _buffering = false;

            for (int i = 0; i < data.Length; i++)
            {
                if (_buffering || _count == 0)
                {
                    _buffering = true;
                    data[i] = 0f;
                    continue;
                }

                data[i] = (float)System.Math.Tanh(_ring[_readIndex] * _gain);
                _readIndex = (_readIndex + 1) % _ring.Length;
                _count--;
            }
        }
    }

    private void OnDestroy()
    {
        VoiceSettings.VolumeChanged -= ApplyVolume;
        if (_source != null && _source.clip != null) Destroy(_source.clip);
    }
}
