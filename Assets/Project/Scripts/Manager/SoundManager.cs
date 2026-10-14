using UnityEngine;
using UnityEngine.Audio;

public enum SoundCategory
{
    Master,
    BGM,
    SFX,
    UI
}

public class SoundManager : GlobalSingleton<SoundManager>
{
    [SerializeField]
    private AudioMixer _mixer;

    [SerializeField]
    private AudioMixerGroup _bgmGroup, _sfxGroup, _uiGroup;

    [SerializeField]
    private int _poolSize = 8;

    [SerializeField]
    private float _maxDistance = 30f;

    private int _nextIndex;

    private AudioSource _bgmSource;
    private AudioSource _sfx2DSource, _uiSource;
    private AudioSource[] _sfx3DPool;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        if (_mixer == null)
        {
            Debug.LogError("[SoundManager] AudioMixer is not assigned. Place the SoundManager prefab in the scene.");
        }

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.outputAudioMixerGroup = _bgmGroup;

        _sfx2DSource = gameObject.AddComponent<AudioSource>();
        _sfx2DSource.playOnAwake = false;
        _sfx2DSource.outputAudioMixerGroup = _sfxGroup;

        _uiSource = gameObject.AddComponent<AudioSource>();
        _uiSource.playOnAwake = false;
        _uiSource.outputAudioMixerGroup = _uiGroup;

        _sfx3DPool = new AudioSource[_poolSize];
        for (int i = 0; i < _poolSize; i++)
        {
            GameObject child = new GameObject($"SFX3D_{i}");
            child.transform.SetParent(transform);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _sfxGroup;
            source.spatialBlend = 1f;                         
            source.rolloffMode = AudioRolloffMode.Linear;     
            source.maxDistance = _maxDistance;

            _sfx3DPool[i] = source;
        }
    }

    private void Start()
    {
        foreach (SoundCategory c in System.Enum.GetValues(typeof(SoundCategory)))
        {
            SetVolume(c, GetVolume(c));
        }
    }

    public void SetVolume(SoundCategory c, float value01)
    {
        value01 = Mathf.Clamp01(value01);
        string key = GetKey(c);

        PlayerPrefs.SetFloat(key, value01);

        if (_mixer != null)
        {
            _mixer.SetFloat(key, ToDecibel(value01));
        }
    }

    public float GetVolume(SoundCategory c)
    {
        return PlayerPrefs.GetFloat(GetKey(c), 1f);
    }

    private static string GetKey(SoundCategory c)
    {
        return c + "Volume";
    }

    private static float ToDecibel(float value01)
    {
        return Mathf.Log10(Mathf.Max(value01, 0.0001f)) * 20f;
    }



    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] Cannot play BGM. Clip is null.");
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            return;
        }

        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] Cannot play SFX. Clip is null.");
            return;
        }

        _sfx2DSource.PlayOneShot(clip);
    }

    public void PlayUI(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] Cannot play UI sound. Clip is null.");
            return;
        }

        _uiSource.PlayOneShot(clip);
    }

    public void PlaySFXAt(AudioClip clip, Vector3 pos)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] Cannot play SFX at position. Clip is null.");
            return;
        }

        AudioSource source = GetFreeSource();
        source.transform.position = pos;
        source.clip = clip;
        source.Play();
    }

    private AudioSource GetFreeSource()
    {
        foreach (AudioSource source in _sfx3DPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        AudioSource oldest = _sfx3DPool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _sfx3DPool.Length;
        return oldest;
    }
}
