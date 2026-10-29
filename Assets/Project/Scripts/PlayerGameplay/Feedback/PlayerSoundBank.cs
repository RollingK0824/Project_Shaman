using System;
using Mirror;
using UnityEngine;
using UnityEngine.Audio;

public enum PlayerSoundCue
{
    DirtStep, WoodStep, Landing, MaleBreath, FemaleBreath, Heartbeat,
    MaleHurt, MaleDeath, FemaleHurt, FemaleDeath, Bell, Salt, Throw,
    Impact, Gong, Coin, MirrorLive, MirrorToggle, Paper, Barrier,
    Talisman, LanternMount, LanternRetrieve, Ignite, Notebook, Drawing, Notice, Equip
}

// Shared Player/Item presentation settings, also used by existing network prefabs.
// No gameplay requests, authority changes or network messages originate here.
[CreateAssetMenu(menuName = "Project Shaman/Player Item Sounds")]
public sealed class PlayerSoundBank : ScriptableObject
{
    [Serializable] public struct Entry
    {
        public PlayerSoundCue Cue;
        [Range(0f, 1f)] public float Volume;
        public AudioClip[] Clips;
    }
    [SerializeField] private Avatar _femaleAvatar;
    [SerializeField] private AudioMixerGroup _sfxGroup, _uiGroup;
    [SerializeField] private Entry[] _sounds;
    private static PlayerSoundBank _default;
    public static PlayerSoundBank Default => _default != null ? _default : _default = Resources.Load<PlayerSoundBank>("PlayerItemSounds");
    public AudioMixerGroup SfxGroup => _sfxGroup;
    public AudioMixerGroup UiGroup => _uiGroup;
    public bool IsFemale(Animator animator) => animator != null && _femaleAvatar != null && animator.avatar == _femaleAvatar;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _default = null;

    public AudioClip[] Clips(PlayerSoundCue cue)
    {
        if (_sounds != null) foreach (var entry in _sounds) if (entry.Cue == cue) return entry.Clips;
        return null;
    }
    public float Volume(PlayerSoundCue cue)
    {
        if (_sounds != null) foreach (var entry in _sounds) if (entry.Cue == cue) return entry.Volume;
        return 0f;
    }
    public AudioClip First(PlayerSoundCue cue)
    {
        var clips = Clips(cue);
        return clips != null && clips.Length > 0 ? clips[0] : null;
    }
    public AudioClip Pick(PlayerSoundCue cue, ref int previous)
    {
        var clips = Clips(cue);
        if (clips == null || clips.Length == 0) return null;
        int index = UnityEngine.Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == previous) index = (index + 1) % clips.Length;
        previous = index;
        return clips[index];
    }

    // Only call on a confirmed visible transition. Dedicated servers stay silent.
    public static void PlayAt(PlayerSoundCue cue, Vector3 position)
    {
        var bank = Default;
        if (bank == null || (NetworkServer.active && !NetworkClient.active) || Application.isBatchMode) return;
        int previous = -1;
        var clip = bank.Pick(cue, ref previous);
        if (clip == null) return;
        var go = new GameObject("Player Item SFX " + cue);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.clip = clip; source.volume = bank.Volume(cue);
        source.outputAudioMixerGroup = bank.SfxGroup;
        source.spatialBlend = 1f; source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1f; source.maxDistance = 14f;
        source.Play();
        Destroy(go, clip.length + .1f);
    }
}
