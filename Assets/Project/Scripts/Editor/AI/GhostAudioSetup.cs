using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using ProjectShaman.Network.AI;

// Puts GhostAudio on the ghost entity prefab with the five ghost sounds and the mixer's SFX group.
// Re-running updates it (a replaced .wav with the same name is picked up as it is)
public static class GhostAudioSetup
{
    private const string GHOST_PATH = "Assets/Project/Prefabs/AI/Ghost/Temp_GhostEntity.prefab";
    private const string AUDIO_FOLDER = "Assets/Project/Audio/Ghost/";
    private const string MIXER_PATH = "Assets/Project/Audio/MainMixer.mixer";
    private const string SFX_GROUP = "SFX";

    [MenuItem("ProjectShaman/AI/Setup Ghost Audio")]
    private static void Setup()
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
        AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups(SFX_GROUP) : null;
        AudioMixerGroup sfx = groups != null && groups.Length > 0 ? groups[0] : null;
        if (sfx == null)
        {
            Debug.LogWarning($"[GhostAudio] No '{SFX_GROUP}' group in {MIXER_PATH}: the sounds play outside the mixer");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(GHOST_PATH);
        try
        {
            GhostAudio audio = root.GetComponent<GhostAudio>();
            if (audio == null)
            {
                audio = root.AddComponent<GhostAudio>();
            }

            SerializedObject so = new SerializedObject(audio);
            so.FindProperty("_sfxGroup").objectReferenceValue = sfx;
            Set(so, "_warningScream", "Ghost_Scream_Warning");
            Set(so, "_attackSlash", "Ghost_Attack_Slash");
            Set(so, "_breathing", "Ghost_Breathing_Loop");
            Set(so, "_proximityDrone", "Ghost_Proximity_Drone");
            Set(so, "_jumpscareStinger", "Ghost_Jumpscare_Stinger");
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, GHOST_PATH);
            Debug.Log($"[GhostAudio] GhostAudio set on {GHOST_PATH} (mixer group {(sfx != null ? sfx.name : "none")})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Set(SerializedObject so, string property, string clipName)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO_FOLDER}{clipName}.wav");
        if (clip == null)
        {
            Debug.LogWarning($"[GhostAudio] Missing {AUDIO_FOLDER}{clipName}.wav");
        }

        so.FindProperty(property).objectReferenceValue = clip;
    }
}
