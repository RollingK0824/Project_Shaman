using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu (fileName = "NetItemDatabase", menuName ="Project Shaman/Net Item Database")]
public class NetItemDataBase : ScriptableObject
{
    [SerializeField] private List<ItemData> _items = new List<ItemData>();

    public int GetId(ItemData itemData)
    {
        return itemData == null ? -1 : _items.IndexOf(itemData);
    }

    public ItemData GetItem(int id)
    {
        return id >= 0 && id < _items.Count ? _items[id] : null;
    }
}
