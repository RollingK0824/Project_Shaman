using System;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class JobEntry
    {
        public string JobId;
        public string DisplayName;
        public AttributeCondition Condition = new AttributeCondition();
    }
}
