using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Dialogue;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    // A player's ritual item reaching a villager: salt sprinkled on them, bean-mugwort thrown, a bell or gong rung nearby.
    // An ordinary villager is only startled (small stress). In a host the ghost shows through in proportion to its yin:
    // low yin looks like anyone startled, enough yin forces trembling, high yin a violent outburst.
    // No animation yet: the reaction is an animator trigger on every peer (Item{Kind}Startled / Symptom*) plus a line.
    [RequireComponent(typeof(AI_Core))]
    public class AI_ItemReactionReceiver : MonoBehaviour, IItemControlReceiver, ISaltReactable, IAIConfigurable
    {
        private const string STARTLED_LINE = "item.startled";
        private const string OUTBURST_LINE = "item.outburst";

        private AI_Core _core;
        private AI_Status _status;
        private AI_Conversation _conversation;
        private AIBehaviourConfig _config;
        private System.Random _random;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _status = GetComponent<AI_Status>();
            _conversation = GetComponent<AI_Conversation>();
            _random = new System.Random(GetInstanceID());
        }

        public void Configure(AIRuntimeContext context)
        {
            _config = context.Config;
        }

        public bool ReactToSalt(GameObject user)
        {
            return React(PlayerItemKind.Salt, user);
        }

        // Bean-mugwort slows, the bell staggers, the gong blurs the senses: a villager takes each only as a fright
        public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
        {
            switch (effect.Kind)
            {
                case ItemControlKind.Slow: return React(PlayerItemKind.BeanMugwort, source);
                case ItemControlKind.Stagger: return React(PlayerItemKind.Bell, source);
                case ItemControlKind.SensoryInterference: return React(PlayerItemKind.Gong, source);
                default: return false;
            }
        }

        private bool React(PlayerItemKind item, GameObject user)
        {
            if (_config == null || _core.Profile == null || (_status != null && _status.IsDead))
            {
                return false;
            }

            string villagerId = _core.LogId;
            string userName = user != null ? user.name : "?";
            AI_Possession possession = _core.Profile.Possession;
            float yin = possession != null ? possession.Yin : 0f;

            AIEvents.RaiseStress(villagerId, $"{item} from {userName}", _config.ItemStartleStress);

            if (possession == null || yin < _config.ItemReactionYin)
            {
                AILog.Log(AILog.GHOST, villagerId, $"Startled by {item} from {userName}{(possession != null ? $" (host, yin {yin:F0} too low to show)" : "")}");
                _core.TriggerEffect($"Item{item}Startled");
                Say(STARTLED_LINE, $"startled by {item}");
                return true;
            }

            bool violent = yin >= _config.ItemViolentYin;
            SymptomType symptom = violent ? SymptomType.Grotesque : SymptomType.Tremble;
            AILog.Log(AILog.GHOST, villagerId, $"Host reacts to {item} from {userName}: {symptom} (yin {yin:F0}{(violent ? ", violent" : "")})");

            if (AIManager.Existing != null)
            {
                AIManager.Existing.ShowSymptom(_core, symptom);
            }
            else
            {
                _core.TriggerSymptom(symptom);
            }

            if (violent)
            {
                Say(OUTBURST_LINE, $"outburst at {item}");
            }

            return true;
        }

        private void Say(string lineId, string reason)
        {
            if (_conversation != null)
            {
                _conversation.Say(DialogueLibrary.Line(lineId, _random), reason);
            }
        }
    }

    public enum PlayerItemKind
    {
        Salt,
        BeanMugwort,
        Bell,
        Gong
    }
}
