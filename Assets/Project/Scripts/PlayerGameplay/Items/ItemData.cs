using UnityEngine;
using UnityEngine.Serialization;
using Exorcist.FirstPerson;

[CreateAssetMenu(
    fileName = "ItemData",
    menuName = "Project Shaman/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string _displayName;
    [SerializeField] private ItemCategory _category;

    [Header("Prefabs")]
    [SerializeField] private GameObject _worldPrefab;

    [Header("First Person Hand")]
    [SerializeField] private HandItemProfile _handProfile;

    [FormerlySerializedAs("_heldPrefab")]
    [SerializeField] private GameObject _visualPrefab;

    public string DisplayName => _displayName;
    public ItemCategory Category => _category;

    public GameObject WorldPrefab => _worldPrefab;
    public GameObject VisualPrefab => _visualPrefab;
    public HandItemProfile HandProfile => _handProfile;
}