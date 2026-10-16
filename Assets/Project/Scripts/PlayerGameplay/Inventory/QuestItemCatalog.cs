using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Quest Item Catalog")]
public class QuestItemCatalog : ScriptableObject
{
    [SerializeField] private ItemData[] _items;
    public ItemData Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _items == null) return null;
        ItemData result = null;
        foreach (var item in _items)
            if (item != null && item.ItemId == id)
            {
                if (result != null && result != item) return null; // Ambiguous IDs never grant a different item.
                result = item;
            }
        return result;
    }
}
