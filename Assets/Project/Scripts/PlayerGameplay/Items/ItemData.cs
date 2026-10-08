using UnityEngine;
using UnityEngine.Serialization;
using Exorcist.FirstPerson;
using ProjectShaman.ItemHold;

[CreateAssetMenu(
    fileName = "ItemData",
    menuName = "Project Shaman/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string _displayName;
    [SerializeField] private ItemCategory _category;

    [Header("World")]
    [SerializeField] private GameObject _worldPrefab;

    [Header("First Person")]
    [SerializeField] private HandItemProfile _handProfile;

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

    public GameObject WorldPrefab => _worldPrefab;

    // 1인칭
    public GameObject VisualPrefab => _visualPrefab;
    public HandItemProfile HandProfile => _handProfile;

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