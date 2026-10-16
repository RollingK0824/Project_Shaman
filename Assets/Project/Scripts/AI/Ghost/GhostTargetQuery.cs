using System;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    [Serializable]
    public struct GhostTargetQuery
    {
        public GhostTargetKind[] Kinds;
        public bool PreferMarked;

        public GhostTargetQuery(bool preferMarked, params GhostTargetKind[] kinds)
        {
            Kinds = kinds;
            PreferMarked = preferMarked;
        }

        public bool IsEmpty => Kinds == null || Kinds.Length == 0;

        public int RankOf(GhostTargetKind kind)
        {
            if (Kinds == null)
            {
                return -1;
            }

            for (int i = 0; i < Kinds.Length; i++)
            {
                if (Kinds[i] == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        public override string ToString()
        {
            return IsEmpty ? "(none)" : string.Join(">", Kinds) + (PreferMarked ? " marked-first" : string.Empty);
        }
    }
}
