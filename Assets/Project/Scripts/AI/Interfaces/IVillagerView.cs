using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Interfaces
{
    public interface IVillagerView
    {
        string VillagerId { get; }
        string DisplayName { get; }
        string JobId { get; }
        string JobName { get; }
        VillagerGender Gender { get; }
        VillagerSocialClass SocialClass { get; }
        VillagerAgeGroup AgeGroup { get; }
        bool IsAlive { get; }
        // Public knowledge of the death (body cleared, or found dead when voted out). Lists and votes use this, not IsAlive
        bool IsDeathKnown { get; }
        VillagerScheduleView Schedule { get; }
    }
}
