using UnityEngine;

// Serialized 0 was Registry. Keep the alias for existing assets/callers.
public enum TalismanType { Effect = 0, Registry = Effect, Barrier = 1 }

[CreateAssetMenu(menuName = "Project Shaman/Talisman Item Data", fileName = "TalismanItemData")]
public class TalismanItemData : ItemData
{
    [SerializeField] private bool _allowDrawing = true;
    [SerializeField] private TalismanType _talismanType;
    [SerializeField] private Texture2D _paperTexture;
    [SerializeField] private TalismanEffectDefinition[] _effects = System.Array.Empty<TalismanEffectDefinition>();
    public System.Collections.Generic.IReadOnlyList<TalismanEffectDefinition> Effects => _effects;
    public bool IsEffectTalisman => _talismanType == TalismanType.Effect;
    public TalismanEffectDefinition FindEffect(string id)
    {
        if (!IsEffectTalisman || string.IsNullOrEmpty(id)) return null;
        foreach (var effect in _effects) if (effect != null && effect.EffectId == id && effect.IsValid) return effect;
        return null;
    }
    public TalismanType TalismanType => _talismanType;
    public Texture2D PaperTexture => _paperTexture;
    public bool AllowDrawing => _allowDrawing && IsEffectTalisman;
}
