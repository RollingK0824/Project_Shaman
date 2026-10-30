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
    // Everyone is startled alike (same line, small stress). In a host the ghost also shows through in proportion to its yin:
    // low yin looks like anyone startled, enough yin forces a quiet symptom at once, high yin twists the body.
    // No animation yet: the reaction is an animator trigger on every peer (Item{Kind}Startled / Symptom*) plus a line.
    [RequireComponent(typeof(AI_Core))]
    public class AI_ItemReactionReceiver : MonoBehaviour, IItemControlReceiver, ISaltReactable, IAIConfigurable
    {
        // Lines per item (item.startled.Salt ...) so the bubble says what just happened to them, host or not
        private const string STARTLED_LINE = "item.startled";

        private AI_Core _core;
        private AI_Status _status;
        private AI_Conversation _conversation;
        private AIBehaviourConfig _config;
        private System.Random _random;
        private readonly ItemReactionGate _gate = new ItemReactionGate();

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
                case ItemControlKind.Stagger: return IsWithinBellStartle(source) && React(PlayerItemKind.Bell, source);
                case ItemControlKind.SensoryInterference: return React(PlayerItemKind.Gong, source);
                default: return false;
            }
        }

        // The bell reaches further than it frightens: only a villager close to the ringer is startled
        private bool IsWithinBellStartle(GameObject source)
        {
            return _config == null || source == null
                || (source.transform.position - transform.position).sqrMagnitude <= _config.BellStartleRadius * _config.BellStartleRadius;
        }

        private bool React(PlayerItemKind item, GameObject user)
        {
            // A sleeper notices nothing: no line, no stress, no symptom, and the item's cooldown stays untouched
            if (_config == null || _core.Profile == null || _core.IsAsleep || (_status != null && _status.IsDead))
            {
                return false;
            }

            // Shaken by this same item a moment ago: it makes no further impression (bell and salt have no cooldown of their own)
            if (!_gate.IsReady(item))
            {
                return false;
            }

            _gate.Consume(item, _config.VillagerItemCooldowns);

            string villagerId = _core.LogId;
            string userName = user != null ? user.name : "?";
            AI_Possession possession = _core.Profile.Possession;
            float yin = possession != null ? possession.Yin : 0f;

            AIEvents.RaiseStress(villagerId, $"{item} from {userName}", _config.ItemStartleStress);

            // Everyone says the same kind of thing: the words never give a host away, only what its body does
            Say($"{STARTLED_LINE}.{item}", $"startled by {item}");

            if (possession == null || yin < _config.ItemReactionYin)
            {
                AILog.Log(AILog.GHOST, villagerId, $"Startled by {item} from {userName}{(possession != null ? $" (host, yin {yin:F0} too low to show)" : "")}");
                _core.TriggerEffect($"Item{item}Startled");
                return true;
            }

            bool violent = yin >= _config.ItemViolentYin;
            SymptomType symptom = PickSymptom(violent);
            AILog.Log(AILog.GHOST, villagerId, $"Host reacts to {item} from {userName}: {symptom} (yin {yin:F0}{(violent ? ", violent" : "")})");

            if (AIManager.Existing != null)
            {
                AIManager.Existing.ShowSymptom(_core, symptom);
            }
            else
            {
                _core.TriggerSymptom(symptom);
            }

            return true;
        }

        // The ghost shows through as one of its usual symptoms: high yin twists the body, otherwise a quieter one
        private SymptomType PickSymptom(bool violent)
        {
            if (violent)
            {
                return SymptomType.Grotesque;
            }

            SymptomType[] quiet = { SymptomType.Tremble, SymptomType.StareIntoVoid, SymptomType.Nonsense };
            return quiet[_random.Next(quiet.Length)];
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
