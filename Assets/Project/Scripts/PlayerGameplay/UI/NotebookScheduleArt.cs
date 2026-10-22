using System;
using UnityEngine;

// Sprites for the notebook villager schedule. Lives in Resources so the runtime-built page can find it;
// the textures stay under Visuals/Textures/NotebookSchedule, the zodiac icons are slices of the ThirdParty Note sheet.
[CreateAssetMenu(menuName = "Project Shaman/UI/Notebook Schedule Art")]
public sealed class NotebookScheduleArt : ScriptableObject
{
    [SerializeField] private Sprite _wash;
    [SerializeField] private Sprite _rule;
    [SerializeField] private Sprite _ruleThin;
    [SerializeField] private Sprite _select;
    [SerializeField] private Sprite _titleUnderline;
    [Tooltip("Twelve sijin animals from 자시 (rat) to 해시 (pig)")]
    [SerializeField] private Sprite[] _zodiac = Array.Empty<Sprite>();

    public Sprite Wash => _wash;
    public Sprite Rule => _rule;
    public Sprite RuleThin => _ruleThin;
    public Sprite Select => _select;
    public Sprite TitleUnderline => _titleUnderline;

    // sijinIndex: 0 = 자시 ... 11 = 해시
    public Sprite Zodiac(int sijinIndex) => sijinIndex >= 0 && sijinIndex < _zodiac.Length ? _zodiac[sijinIndex] : null;
}
