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
        Accumulation
    }

    public enum PlaceShape
    {
        Sphere,
        Box
    }
}
