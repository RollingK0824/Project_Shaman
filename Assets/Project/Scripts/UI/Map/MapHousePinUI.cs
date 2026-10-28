using ProjectShaman.AI.World;
using ProjectShaman.Map;
using UnityEngine;
using UnityEngine.EventSystems;

// A house icon placed by hand on the map picture. Shown only while the house has an owner, name on hover
public class MapHousePinUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    private string _placeId;    // the PlaceArea's PlaceId (e.g. Home_07), not the object name

    private MapTab _owner;
    private string _label;
    private bool _warned;

    public void Refresh(MapTab owner)
    {
        _owner = owner;

        if (!MapAreaRegistry.TryGet(_placeId, out IMapArea area) || !(area is PlaceArea place))
        {
            if (!_warned)
            {
                Debug.LogWarning($"[MapHousePin] No house with PlaceId '{_placeId}' on '{name}'", this);
                _warned = true;
            }

            gameObject.SetActive(false);
            return;
        }

        bool owned = !string.IsNullOrEmpty(place.OwnerId);
        _label = owned ? place.DisplayName : null;
        gameObject.SetActive(owned);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_owner != null) _owner.ShowTooltip(_label, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_owner != null) _owner.HideTooltip();
    }
}
