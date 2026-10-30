using UnityEngine;


// [모든 클라] 받은 음성을 링 버퍼에 쌓아 두고 스트리밍 AudioClip으로 꺼내서 재생
// 근거리용(3D)과 무전용(2D + 필터) 오브젝트에 하나씩 붙임

[RequireComponent(typeof(AudioSource))]
public class VoicePlayback : MonoBehaviour
{
    [SerializeField, Min(.02f)] private float _startDelay = .12f;
    [SerializeField, Min(.2f)] private float _bufferSeconds = 1f;

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

    // Init 전 (Steam 미초기화 등)에는 _source가 없음. 값만 저장해 두고 Init에서 적용
    private void ApplyVolume(float volume)
    {
        if (_source != null) _source.volume = volume * _playerVolume;
    }

    // 무전 신호음. 같은 AudioSource로 재생해서 필터와 음성 볼륨이 그대로 적용
    public void PlayCue(AudioClip clip, float volumeScale)
    {
        if (_source != null && clip != null) _source.PlayOneShot(clip, volumeScale);
    }

    // [메인 스레드] Steam DecompressVoice 결과 (16비트 모노 PCM, 리틀 엔디언)
    public void Push(byte[] pcm16, int byteCount)
    {
        if (!IsInitialized) return;

        lock (_lock)
        {
            for (int i = 0; i + 1 < byteCount; i += 2)
            {
                // 가득 차면 가장 오래된 샘플을 버림 (지연이 계속 쌓이는 것 방지){
                if (_count == _ring.Length)
                {
                    _readIndex = (_readIndex + 1) % _ring.Length;
                    _count--;
                }

                short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
                _ring[_writeIndex] = sample / 32768f;
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

                data[i] = _ring[_readIndex];
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
