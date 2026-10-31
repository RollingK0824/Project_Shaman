using System;
using System.Collections.Generic;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    // One possession symptom: how much yin it takes to show, how likely it is among the symptoms the host can show,
    // where it may happen (behaviour) or how long it lasts (appearance). Tunable in AIBehaviourConfig.
    [Serializable]
    public class SymptomRule
    {
        public SymptomType Type;
        [UnityEngine.Tooltip("The symptom shows only from this host yin on (weak symptoms low, frightening ones high)")]
        public float MinYin = 20f;
        [UnityEngine.Tooltip("Behaviour: relative chance among the symptoms the yin and context allow. Appearance: multiplier on the episode start chance")]
        public float Weight = 1f;
        [UnityEngine.Tooltip("Behaviour symptoms only: where it may happen")]
        public SymptomContext Context = SymptomContext.Moving | SymptomContext.Resting;
        [UnityEngine.Tooltip("A weak symptom a host may show when startled by a player's item")]
        public bool ItemReaction;
        [UnityEngine.Tooltip("Appearance symptoms: each episode lasts between these seconds")]
        public float MinSeconds = 20f;
        public float MaxSeconds = 60f;

        public bool IsAppearance => Type == SymptomType.NoShadow || Type == SymptomType.PaleSkin || Type == SymptomType.BlackEyes;
        public bool IsBehaviour => !IsAppearance && Type != SymptomType.EmptyHandTool;

        public SymptomRule() { }

        public SymptomRule(SymptomType type, float minYin, float weight, SymptomContext context, bool itemReaction, float minSeconds = 0f, float maxSeconds = 0f)
        {
            Type = type;
            MinYin = minYin;
            Weight = weight;
            Context = context;
            ItemReaction = itemReaction;
            MinSeconds = minSeconds;
            MaxSeconds = maxSeconds;
        }

        public static List<SymptomRule> CreateDefaults()
        {
            const SymptomContext Both = SymptomContext.Moving | SymptomContext.Resting;
            return new List<SymptomRule>
            {
                // Behaviour: quiet first, the body twisting only at high yin
                new SymptomRule(SymptomType.Tremble, 20f, 3f, SymptomContext.Moving, true),
                new SymptomRule(SymptomType.StareIntoVoid, 20f, 3f, SymptomContext.Resting, true),
                new SymptomRule(SymptomType.Nonsense, 30f, 2f, SymptomContext.Resting, true),
                new SymptomRule(SymptomType.NeckTwist, 50f, 2f, Both, true),
                new SymptomRule(SymptomType.WaistBend, 65f, 1.5f, SymptomContext.Resting, false),
                new SymptomRule(SymptomType.Grotesque, 80f, 1f, SymptomContext.Moving, false),
                // Appearance: a pale face is easy to dismiss, black eyes are not, no shadow is the surest sign
                new SymptomRule(SymptomType.PaleSkin, 30f, 1f, SymptomContext.None, false, 20f, 60f),
                new SymptomRule(SymptomType.BlackEyes, 50f, 0.7f, SymptomContext.None, false, 10f, 30f),
                new SymptomRule(SymptomType.NoShadow, 70f, 0.5f, SymptomContext.None, false, 15f, 40f)
            };
        }
    }
}
