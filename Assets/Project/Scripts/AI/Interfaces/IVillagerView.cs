using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Interfaces
{
    public interface IVillagerView
    {
        string VillagerId { get; }
        string DisplayName { get; }
        string JobId { get; }
        VillagerGender Gender { get; }
        VillagerSocialClass SocialClass { get; }
        VillagerAgeGroup AgeGroup { get; }
        bool IsAlive { get; }
        VillagerScheduleView Schedule { get; }
    }
}
