using System;
using System.Collections.Generic;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class AttributeCondition
    {
        public List<VillagerGender> Genders = new List<VillagerGender>();
        public List<VillagerSocialClass> SocialClasses = new List<VillagerSocialClass>();
        public List<VillagerAgeGroup> AgeGroups = new List<VillagerAgeGroup>();

        public bool Matches(VillagerGender gender, VillagerSocialClass socialClass, VillagerAgeGroup ageGroup)
        {
            return IsAllowed(Genders, gender) && IsAllowed(SocialClasses, socialClass) && IsAllowed(AgeGroups, ageGroup);
        }

        private static bool IsAllowed<T>(List<T> allowed, T value)
        {
            return allowed == null || allowed.Count == 0 || allowed.Contains(value);
        }
    }
}
