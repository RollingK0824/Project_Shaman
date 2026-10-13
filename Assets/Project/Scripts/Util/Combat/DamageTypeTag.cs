using UnityEngine;

public class DamageTypeTag : MonoBehaviour, IDamageTypeProvider
{
    [SerializeField] private DamageType _damageType = DamageType.Physical;

    public DamageType DamageType => _damageType;

    public static DamageType Resolve(GameObject source)
    {
        if (source == null)
        {
            return DamageType.Physical;
        }

        IDamageTypeProvider provider = source.GetComponentInParent<IDamageTypeProvider>();
        return provider != null ? provider.DamageType : DamageType.Physical;
    }
}
