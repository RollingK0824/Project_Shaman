using Unity.Behavior;

namespace ProjectShaman.AI.Defines
{
    [BlackboardEnum]
    public enum RoutineCategory
    {
        None,
        Work,
        Rest
    }

    public enum PlaceType
    {
        Work,
        Rest,
        Storage,
        Home,
        Accumulation,
        Area
    }

    public enum PlaceShape
    {
        Sphere,
        Box
    }

    // Values are the Animator's WorkAction integer: keep them fixed when adding more
    public enum WorkAnimation
    {
        None = 0,
        Farming = 1,
        Gathering = 2
    }
}
