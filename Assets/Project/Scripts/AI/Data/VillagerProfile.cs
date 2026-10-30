using System;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public class VillagerPublicInfo
    {
        public string VillagerId;
        public string DisplayName;
        public string JobId;
        public string JobName;
        public VillagerGender Gender;
        public VillagerSocialClass SocialClass;
        public VillagerAgeGroup AgeGroup;
        public string HomePlaceId;

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
        // Home points that share a parent object in the scene are one neighbourhood (set once at creation)
        public string NeighborhoodId;
        public VillagerSchedule Schedule;
        public bool IsPossessed;
        public string GhostId;
        public GhostType GhostType;
        public ProjectShaman.AI.Ghost.AI_Possession Possession;
        // Day the ghost was driven out of this villager, -1 = never (thanks the players from the next day)
        public int ExorcisedDay = -1;

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
