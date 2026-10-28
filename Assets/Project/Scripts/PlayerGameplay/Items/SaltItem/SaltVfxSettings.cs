using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Items/Salt VFX Settings")]
public sealed class SaltVfxSettings : ScriptableObject
{
    public Material GrainMaterial, DustMaterial;
    [Header("Spray (world units, independent of held model scale)")]
    [Range(8, 160)] public int SprayGrains = 110;
    [Range(0, 60)] public int SprayPowder = 30;
    public Vector2 GrainSize = new Vector2(.0035f, .008f);
    public Vector2 SpraySpeed = new Vector2(3.8f, 8.2f);
    [Header("Confirmed ghost contact")]
    [Range(4, 80)] public int ImpactGrains = 32;
    [Range(0, 32)] public int ImpactPowder = 12;
    [Range(.05f, .5f)] public float PowderOpacity = .22f;
    [Header("Budget")]
    [Range(4, 48)] public int MaxLiveBursts = 24;
    private static SaltVfxSettings _default;
    public static SaltVfxSettings Default => _default != null ? _default : _default = Resources.Load<SaltVfxSettings>("SaltVfxSettings");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _default = null;
}
