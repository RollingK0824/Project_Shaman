using UnityEngine;

// Existing numeric values remain readable for older definitions and integrations.
public enum TalismanEffectType { MovementSpeed, StressGain, StressRecovery, StressRelief, HealthRestore, VillagerTracking }
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
    public string Description => _effectType switch
    {
        TalismanEffectType.VillagerTracking => "주민에게 부착하면 다음 날 아침까지 위치를 표시합니다. 다른 주민에게 새 추적 부적을 사용하면 대상만 변경되며, 만료 시각은 연장되지 않습니다.",
        TalismanEffectType.MovementSpeed => $"{_duration:0.#}초 동안 이동속도가 {_magnitude:0.##}배가 됩니다.",
        TalismanEffectType.StressRelief => $"사용 즉시 스트레스가 {_magnitude:0.#} 감소합니다.",
        TalismanEffectType.HealthRestore => $"사용 즉시 체력이 {_magnitude:0.#} 회복됩니다.",
        _ => _description
    };
    public bool IsInstant => _effectType == TalismanEffectType.StressRelief || _effectType == TalismanEffectType.HealthRestore;
    public TalismanEffectType EffectType => _effectType;
    public float Magnitude => _magnitude;
    public float Duration => _duration;
    public TalismanStackPolicy StackPolicy => _stackPolicy;
    public bool ConsumeOnUse => _consumeOnUse;
    // Read from the same Inspector values used by gameplay so descriptions cannot drift.
    public string ModifierDescription => _effectType switch
    {
        TalismanEffectType.MovementSpeed => $"이동속도 ×{_magnitude:0.##}",
        TalismanEffectType.StressGain => $"받는 스트레스 ×{_magnitude:0.##}",
        TalismanEffectType.StressRecovery => $"스트레스 회복량 ×{_magnitude:0.##}",
        TalismanEffectType.StressRelief => $"스트레스 -{_magnitude:0.#}",
        TalismanEffectType.HealthRestore => $"체력 +{_magnitude:0.#}",
        _ => _displayName
    };
    public bool IsValid => !string.IsNullOrWhiteSpace(_effectId) && _effectId.Length <= 64 &&
        !float.IsNaN(_magnitude) && !float.IsInfinity(_magnitude) && _magnitude > 0f && _magnitude <= (IsInstant ? 10000f : 10f) &&
        !float.IsNaN(_duration) && !float.IsInfinity(_duration) && _duration > 0f &&
        System.Enum.IsDefined(typeof(TalismanEffectType), _effectType) &&
        System.Enum.IsDefined(typeof(TalismanStackPolicy), _stackPolicy);
}
