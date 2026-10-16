using UnityEngine;

public enum TalismanEffectType { MovementSpeed, StressGain, StressRecovery }
public enum TalismanStackPolicy { RefreshDuration, RejectWhileActive }

[CreateAssetMenu(menuName = "Project Shaman/Talisman Effect", fileName = "TalismanEffect")]
public class TalismanEffectDefinition : ScriptableObject
{
    [SerializeField] private string _effectId;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private TalismanEffectType _effectType;
    [SerializeField, Min(0f)] private float _magnitude = 1.2f;
    [SerializeField, Min(.01f)] private float _duration = 15f;
    [SerializeField] private TalismanStackPolicy _stackPolicy;
    [SerializeField] private bool _consumeOnUse = true;
    public string EffectId => _effectId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public TalismanEffectType EffectType => _effectType;
    public float Magnitude => _magnitude;
    public float Duration => _duration;
    public TalismanStackPolicy StackPolicy => _stackPolicy;
    public bool ConsumeOnUse => _consumeOnUse;
    public bool IsValid => !string.IsNullOrWhiteSpace(_effectId) && _effectId.Length <= 64 &&
        !float.IsNaN(_magnitude) && !float.IsInfinity(_magnitude) && _magnitude >= 0f && _magnitude <= 10f &&
        !float.IsNaN(_duration) && !float.IsInfinity(_duration) && _duration > 0f &&
        System.Enum.IsDefined(typeof(TalismanEffectType), _effectType) &&
        System.Enum.IsDefined(typeof(TalismanStackPolicy), _stackPolicy);
}
