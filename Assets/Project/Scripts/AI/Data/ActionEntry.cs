using System;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    // One work action and the animation it plays; several actions may share an animation (till and chop both swing)
    [Serializable]
    public class ActionEntry
    {
        public string ActionId;
        public WorkAnimation Animation;
    }
}
