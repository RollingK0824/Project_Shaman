using System;

namespace ProjectShaman.AI.Data
{
    // One work action and the Animator state it plays; several actions may share one (till and chop both swing)
    [Serializable]
    public class ActionEntry
    {
        public string ActionId;
        public string Animation;
    }
}
