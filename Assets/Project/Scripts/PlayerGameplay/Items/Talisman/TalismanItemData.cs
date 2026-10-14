using UnityEngine;

public enum TalismanType { Registry, Barrier }

[CreateAssetMenu(menuName = "Project Shaman/Talisman Item Data", fileName = "TalismanItemData")]
public class TalismanItemData : ItemData
{
    [SerializeField] private bool _allowDrawing = true;
    [SerializeField] private TalismanType _talismanType;
    [SerializeField] private Texture2D _paperTexture;
    public TalismanType TalismanType => _talismanType;
    public Texture2D PaperTexture => _paperTexture;
    public bool AllowDrawing => _allowDrawing && _talismanType == TalismanType.Registry;
}
