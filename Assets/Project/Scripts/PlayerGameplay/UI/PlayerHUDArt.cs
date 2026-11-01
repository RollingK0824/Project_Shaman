using TMPro;
using UnityEngine;

// The same authored brush sprites and font as DayPhaseHUD, available to runtime HUDs.
public sealed class PlayerHUDArt : ScriptableObject
{
    [SerializeField] private Sprite _inkWash;
    [SerializeField] private Sprite _redBrush;
    [SerializeField] private TMP_FontAsset _font;
    private static PlayerHUDArt _instance;
    public static PlayerHUDArt Current => _instance != null ? _instance : _instance = Resources.Load<PlayerHUDArt>("PlayerHUDArt");
    public Sprite InkWash => _inkWash;
    public Sprite RedBrush => _redBrush;
    public TMP_FontAsset Font => _font;
    public static readonly Color Text = new(.96f, .96f, .94f, 1f);
}
