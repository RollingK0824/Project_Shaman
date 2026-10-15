using UnityEngine;
using UnityEngine.Serialization;
using ProjectShaman.ItemHold;

[CreateAssetMenu(
    fileName = "ItemData",
    menuName = "Project Shaman/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string _displayName;
    [Tooltip("Stable content ID: item_<lowercase_snake_case>. Shared with quest TargetId / Reward.Id.")]
    [SerializeField] private string _itemId;
    public string ItemId => _itemId;
    [SerializeField] private ItemCategory _category;
    [SerializeField] private Sprite _icon;

    [Header("Ownership")]
    [SerializeField] private bool _isPermanent;
    [SerializeField] private bool _isUnique;
    [SerializeField] private bool _canUseQuickSlot = true;
    public bool IsPermanent => _isPermanent;
    public bool IsUnique => _isUnique;

    [Header("World")]
    [SerializeField] private GameObject _worldPrefab;

    [Header("First Person")]
    [SerializeField] private Vector3 _firstPersonLocalPosition;
    [SerializeField] private Vector3 _firstPersonLocalEuler;
    [SerializeField] private Vector3 _firstPersonLocalScale = Vector3.one;
    [Tooltip("Normalize the longest mesh dimension at equip time. Zero preserves authored scale.")]
    [SerializeField, Min(0f)] private float _firstPersonDisplaySize = .28f;

    public Vector3 FirstPersonLocalPosition => _firstPersonLocalPosition;
    public Vector3 FirstPersonLocalEuler => _firstPersonLocalEuler;
    public Vector3 FirstPersonLocalScale => _firstPersonLocalScale;
    public float FirstPersonDisplaySize => _firstPersonDisplaySize;

    [FormerlySerializedAs("_heldPrefab")]
    [SerializeField] private GameObject _visualPrefab;

    [Header("Third Person Hold")]
    [SerializeField] private HoldType _holdType = HoldType.Empty;

    [Tooltip(
        "3인칭에서 손에 표시할 전용 프리팹. " +
        "비워두면 Visual Prefab을 대신 사용합니다."
    )]
    [SerializeField] private GameObject _thirdPersonVisualPrefab;

    [SerializeField] private Vector3 _thirdPersonLocalPosition;
    [SerializeField] private Vector3 _thirdPersonLocalEuler;

    [SerializeField]
    private Vector3 _thirdPersonLocalScale = Vector3.one;

    [Header("Third Person Use")]
    [Tooltip(
        "0 = 별도 사용 애니메이션 없음\n" +
        "1 = Bell\n" +
        "2 = Observe"
    )]
    [SerializeField] private int _thirdPersonUseMotionId;

    public string DisplayName => _displayName;
    public ItemCategory Category => _category;
    public bool CanUseQuickSlot => _category != ItemCategory.Material && _canUseQuickSlot;
    public Sprite Icon => _icon;

    public GameObject WorldPrefab => _worldPrefab;

    // 1인칭
    public GameObject VisualPrefab => _visualPrefab;

    // 3인칭
    public HoldType HoldType => _holdType;

    public GameObject ThirdPersonVisualPrefab =>
        _thirdPersonVisualPrefab != null
            ? _thirdPersonVisualPrefab
            : _visualPrefab;

    public Vector3 ThirdPersonLocalPosition =>
        _thirdPersonLocalPosition;

    public Vector3 ThirdPersonLocalEuler =>
        _thirdPersonLocalEuler;

    public Vector3 ThirdPersonLocalScale =>
        _thirdPersonLocalScale;

    public int ThirdPersonUseMotionId =>
        _thirdPersonUseMotionId;
}
