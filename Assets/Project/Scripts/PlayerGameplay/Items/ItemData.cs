using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemData",
    menuName = "Project Shaman/Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string _displayName;

    [Header("Prefabs")]
    [SerializeField] private GameObject _worldPrefab;
    [SerializeField] private GameObject _heldPrefab;

    [Header("Spawn")]
    [SerializeField] private float _spawnWeight = 1f;

    public string DisplayName => _displayName;
    public GameObject WorldPrefab => _worldPrefab;
    public GameObject HeldPrefab => _heldPrefab;
    public float SpawnWeight => _spawnWeight;

    private void OnValidate()
    {
        if (_spawnWeight < 0f)
        {
            _spawnWeight = 0f;
        }
    }
}