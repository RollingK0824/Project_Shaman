using System;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class VillagerPublicInfo
    {
        public string VillagerId;
        public string DisplayName;
        public string JobId;
        public VillagerGender Gender;
        public VillagerSocialClass SocialClass;
        public VillagerAgeGroup AgeGroup;

        public override string ToString()
        {
            return $"{VillagerId} {DisplayName}/{JobId}/{Gender}/{SocialClass}/{AgeGroup}";
        }
    }

    public class VillagerProfile
    {
        public int Index { get; }
        public VillagerPublicInfo PublicInfo { get; }

        public int HouseIndex = -1;
        public Vector3 HomePosition;
        public Vector3 InitialWorkPosition;
        public float StartOffsetSeconds;

        public string VillagerId => PublicInfo.VillagerId;

        public VillagerProfile(int index)
        {
            Index = index;
            PublicInfo = new VillagerPublicInfo
            {
                VillagerId = $"NPC_{index:00}"
            };
        }
    }
}
