using System;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class NameEntry
    {
        public string Name;
        public AttributeCondition Condition = new AttributeCondition();
    }
}
