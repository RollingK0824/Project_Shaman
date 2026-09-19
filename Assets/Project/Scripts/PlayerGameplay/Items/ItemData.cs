using UnityEngine;

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
    [SerializeField] private GameObject _heldPrefab;

    public string DisplayName => _displayName;
    public ItemCategory Category => _category;

    public GameObject WorldPrefab => _worldPrefab;
    public GameObject HeldPrefab => _heldPrefab;
}