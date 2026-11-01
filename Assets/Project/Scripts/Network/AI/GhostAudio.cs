using UnityEngine;
using UnityEngine.Audio;
using ProjectShaman.AI;

namespace ProjectShaman.Network.AI
{
    // [Every peer, local presentation] The ghost's sounds, all through the mixer's SFX group:
    // - a warning scream heard everywhere as an awakened ghost (full yin) comes out
    // - a slash where it is as it swings at a player (Net_GhostEntity.ClientAttackSwung)
    // - a low drone around it while it is out (3D, fades with distance) and a breath now and then when this player is near
    // - a stinger once a night when this player suddenly finds it close in front, not behind a wall
    // Clips are set by ProjectShaman/AI/Setup Ghost Audio
    [RequireComponent(typeof(Net_GhostEntity))]
    public class GhostAudio : MonoBehaviour
    {
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("Clips")]
        [SerializeField] private AudioClip _warningScream;
        [SerializeField] private AudioClip _attackSlash;
        [SerializeField] private AudioClip _breathing;
        [SerializeField] private AudioClip _proximityDrone;
        [SerializeField] private AudioClip _jumpscareStinger;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _screamVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _attackVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _breathVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _droneVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _stingerVolume = 0.9f;

        [Header("Drone (3D loop)")]
        [Tooltip("Full volume within this distance (m)")]
        [SerializeField, Min(0.1f)] private float _droneMinDistance = 2f;
        [Tooltip("Silent beyond this distance (m)")]
        [SerializeField, Min(0.5f)] private float _droneMaxDistance = 20f;

        [Header("Breathing")]
        [Tooltip("Breathes only while the local player is this close (m)")]
        [SerializeField, Min(0f)] private float _breathRange = 12f;
        [SerializeField, Min(0.5f)] private float _breathIntervalMin = 6f;
        [SerializeField, Min(0.5f)] private float _breathIntervalMax = 14f;

        [Header("Jumpscare")]
        [Tooltip("The stinger plays when the ghost is this close (m) and in front of the local camera")]
        [SerializeField, Min(0f)] private float _stingerDistance = 6f;
        [Tooltip("How straight ahead it must be: dot of the camera forward and the direction to the ghost")]
        [SerializeField, Range(0f, 1f)] private float _stingerViewDot = 0.8f;
        [Tooltip("Walls and terrain that hide it (empty = no line-of-sight test)")]
        [SerializeField] private LayerMask _stingerOcclusion = 0;

        private const float LOOK_HEIGHT = 1.3f;

        private Net_GhostEntity _net;
        private AudioSource _drone;
        private AudioSource _voice;
        private float _nextBreathTime;
        private bool _hasScreamed;
        private bool _hasStung;

        private void Awake()
        {
            _net = GetComponent<Net_GhostEntity>();
            _drone = CreateSource(true, _droneMinDistance, _droneMaxDistance);
            _voice = CreateSource(false, _droneMinDistance, _droneMaxDistance);
        }

        private void OnEnable()
        {
            _net.ClientAttackSwung += PlayAttack;
            _nextBreathTime = Time.time + Random.Range(_breathIntervalMin, _breathIntervalMax);
        }

        private void OnDisable()
        {
            _net.ClientAttackSwung -= PlayAttack;
        }

        private void Start()
        {
            if (_proximityDrone != null)
            {
                _drone.clip = _proximityDrone;
                _drone.volume = _droneVolume;
                _drone.Play();
            }
        }

        private void Update()
        {
            // The synced flag may arrive a frame after spawn
            if (!_hasScreamed && _net.IsAwakened)
            {
                _hasScreamed = true;
                Play2D(_warningScream, _screamVolume);
            }

            Camera viewer = AI_ViewerCamera.Current;
            if (viewer == null)
            {
                return;
            }

            Vector3 eye = viewer.transform.position;
            Vector3 head = transform.position + Vector3.up * LOOK_HEIGHT;
            float distance = Vector3.Distance(eye, head);

            if (Time.time >= _nextBreathTime)
            {
                _nextBreathTime = Time.time + Random.Range(_breathIntervalMin, _breathIntervalMax);
                if (_breathing != null && distance <= _breathRange)
                {
                    _voice.PlayOneShot(_breathing, _breathVolume);
                }
            }

            if (!_hasStung && _jumpscareStinger != null && distance <= _stingerDistance
                && Vector3.Dot(viewer.transform.forward, (head - eye).normalized) >= _stingerViewDot
                && (_stingerOcclusion.value == 0 || !Physics.Linecast(eye, head, _stingerOcclusion, QueryTriggerInteraction.Ignore)))
            {
                _hasStung = true;
                Play2D(_jumpscareStinger, _stingerVolume);
            }
        }

        private void PlayAttack()
        {
            if (_attackSlash != null)
            {
                _voice.PlayOneShot(_attackSlash, _attackVolume);
            }
        }

        private AudioSource CreateSource(bool loop, float minDistance, float maxDistance)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = _sfxGroup;
            return source;
        }

        // Heard the same everywhere (not placed in the world); outlives the ghost so a scream is not cut off at morning
        private void Play2D(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            GameObject holder = new GameObject($"GhostAudio_{clip.name}");
            AudioSource source = holder.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = _sfxGroup;
            source.clip = clip;
            source.volume = volume;
            source.Play();
            Destroy(holder, clip.length + 0.1f);
        }
    }
}
