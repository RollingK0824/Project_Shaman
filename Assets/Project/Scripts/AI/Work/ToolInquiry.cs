using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Work
{
    // Server only. After a failed tool search the villager walks to a nearby villager and asks (9.2).
    // The approach rides on AI_Interaction (BT interrupt branch); the answer is shared into the asker's memory,
    // so the next search plan picks it up as a candidate.
    public class ToolInquiry
    {
        private const float INQUIRY_RADIUS = 15f;
        private const float HOLD_SECONDS = 8f;
        private const float APPROACH_TIMEOUT = 12f;
        private const float ARRIVE_MARGIN = 0.8f;
        private const float LINE_GAP = 1.6f;
        private const float FALLBACK_HEARING = 4f;

        private enum Phase
        {
            None,
            Approaching,
            Asked,
            Answered
        }

        private readonly AI_ToolHandler _owner;
        private readonly AI_Interaction _interaction;
        private readonly AI_Conversation _conversation;

        private Phase _phase;
        private AI_Conversation _partner;
        private string _toolType;
        private int _serial;
        private float _nextTime;
        private float _approachDeadline;
        private bool _gotAnswer;
        private int _askedRoutineSerial = -1;
        private Vector3 _meetPoint;
        private int _askedDay = -1;
        private readonly HashSet<string> _askedToday = new HashSet<string>();

        public bool IsActive => _phase != Phase.None;
        public string PartnerId => _partner != null ? _partner.VillagerId : null;

        private string Id => _owner.VillagerId;

        public ToolInquiry(AI_ToolHandler owner)
        {
            _owner = owner;
            _interaction = owner.GetComponent<AI_Interaction>();
            _conversation = owner.GetComponent<AI_Conversation>();
        }

        // Once per routine; false means the caller should give up and raise the request
        public bool TryStart(string toolType)
        {
            int routineSerial = _owner.Core.CurrentRoutine.Serial;
            if (IsActive || _interaction == null || _conversation == null || routineSerial == _askedRoutineSerial)
            {
                return false;
            }

            RefreshAskedDay();
            AI_Conversation partner = FindPartner(toolType);
            if (partner == null)
            {
                AILog.Log(AILog.TOOL, Id, $"Inquiry for '{toolType}' skipped (nobody within {INQUIRY_RADIUS:F0}m not yet asked today)");
                return false;
            }

            if (!_interaction.TryBegin(partner, InteractionResponse.Approach, HOLD_SECONDS))
            {
                AILog.Log(AILog.TOOL, Id, $"Inquiry for '{toolType}' skipped (interaction busy)");
                return false;
            }

            _askedRoutineSerial = routineSerial;
            _askedToday.Add(AskedKey(toolType, partner.VillagerId));
            _meetPoint = partner.transform.position;
            _partner = partner;
            _toolType = toolType;
            _serial = _interaction.Serial;
            _gotAnswer = false;
            _approachDeadline = Time.time + APPROACH_TIMEOUT;
            _phase = Phase.Approaching;
            AILog.Log(AILog.TOOL, Id, $"Inquiry for '{toolType}' → walking to {partner.VillagerId}");
            return true;
        }

        public void Tick()
        {
            if (!IsActive)
            {
                return;
            }

            if (_partner == null || !_interaction.IsInteracting || _interaction.Serial != _serial)
            {
                Finish("interrupted");
                return;
            }

            switch (_phase)
            {
                case Phase.Approaching:
                    TickApproach();
                    break;
                case Phase.Asked:
                    if (Time.time >= _nextTime)
                    {
                        Answer();
                    }
                    break;
                case Phase.Answered:
                    if (Time.time >= _nextTime)
                    {
                        _conversation.Say(_gotAnswer ? "고맙소, 가 보리다" : "허, 이거 큰일이구먼", "inquiry reply");
                        int serial = _serial;
                        Finish(_gotAnswer ? "got a lead" : "no lead");
                        _interaction.CompleteActive(serial);
                    }
                    break;
            }
        }

        public void Cancel(string reason)
        {
            if (IsActive)
            {
                Finish(reason);
            }
        }

        private void TickApproach()
        {
            float arrive = (_partner.Profile != null ? _partner.Profile.ApproachDistance : 2f) + ARRIVE_MARGIN;
            float hearing = _partner.HearingDistance > 0f ? _partner.HearingDistance : FALLBACK_HEARING;
            float toPartner = FlatDistance(_partner.transform.position);
            bool atMeetPoint = FlatDistance(_meetPoint) <= arrive;

            if (toPartner > arrive && !(atMeetPoint && toPartner <= hearing))
            {
                if (atMeetPoint || Time.time > _approachDeadline)
                {
                    AILog.Log(AILog.TOOL, Id, $"Inquiry gave up reaching {_partner.VillagerId} ({toPartner:F1}m, {(atMeetPoint ? "partner moved away" : "timeout")})");
                    int serial = _serial;
                    Finish("could not reach");
                    _interaction.CompleteActive(serial);
                }

                return;
            }

            _conversation.Say($"{_partner.DisplayName}, 혹시 {AIDisplayNames.Tool(_toolType)} 못 봤소?", $"inquiry to {_partner.VillagerId}");
            _phase = Phase.Asked;
            _nextTime = Time.time + LINE_GAP;
        }

        private void Answer()
        {
            AI_Memory partnerMemory = _partner.GetComponent<AI_Memory>();
            _gotAnswer = partnerMemory != null && TryGetLatest(partnerMemory.ToolSightings.Get(_toolType), out ToolSighting sighting)
                && Share(sighting);

            if (!_gotAnswer)
            {
                _partner.Say("못 봤는디유", $"inquiry answer to {Id}");
            }

            _phase = Phase.Answered;
            _nextTime = Time.time + LINE_GAP;
        }

        private bool Share(ToolSighting sighting)
        {
            _owner.Memory.ToolSightings.Record(sighting);
            _partner.Say(DescribeSighting(sighting), $"inquiry answer to {Id}");
            AILog.Log(AILog.MEMORY, Id, $"Heard from {_partner.VillagerId}: {sighting}");
            return true;
        }

        private bool TryGetLatest(IReadOnlyList<ToolSighting> sightings, out ToolSighting latest)
        {
            latest = default;
            bool found = false;

            foreach (ToolSighting sighting in sightings)
            {
                if (sighting.HolderId == Id)
                {
                    continue;
                }

                if (!found || sighting.SeenTime > latest.SeenTime)
                {
                    latest = sighting;
                    found = true;
                }
            }

            return found;
        }

        private string DescribeSighting(ToolSighting sighting)
        {
            string tool = AIDisplayNames.Tool(sighting.TypeId);

            if (!string.IsNullOrEmpty(sighting.HolderId))
            {
                string holder = DisplayNameOf(sighting.HolderId);
                return $"{holder}{AIDisplayNames.Josa(holder, "이", "가")} {tool}{AIDisplayNames.Josa(tool, "을", "를")} 들고 있더구먼";
            }

            if (PlaceRegistry.TryFindContaining(sighting.Position, out PlaceArea place))
            {
                return $"{place.DisplayName} 쪽에 {tool}{AIDisplayNames.Josa(tool, "이", "가")} 놓여 있더구먼";
            }

            return $"저짝에 {tool}{AIDisplayNames.Josa(tool, "이", "가")} 떨어져 있던디";
        }

        private static string DisplayNameOf(string villagerId)
        {
            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            return registry != null && registry.TryGet(villagerId, out IVillagerView view) && !string.IsNullOrEmpty(view.DisplayName) ? view.DisplayName : villagerId;
        }

        private float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - _owner.transform.position;
            return new Vector2(offset.x, offset.z).magnitude;
        }

        private static string AskedKey(string toolType, string partnerId)
        {
            return $"{toolType}:{partnerId}";
        }

        private void RefreshAskedDay()
        {
            int day = _owner.Core.CurrentRoutine.DayCount;
            if (day != _askedDay)
            {
                _askedDay = day;
                _askedToday.Clear();
            }
        }

        // Nearest simulating villager not yet asked about this tool today
        private AI_Conversation FindPartner(string toolType)
        {
            AI_Conversation best = null;
            float bestSqr = INQUIRY_RADIUS * INQUIRY_RADIUS;
            Vector3 from = _owner.transform.position;

            foreach (AI_Conversation candidate in AI_Conversation.All)
            {
                if (candidate == null || candidate == _conversation || candidate.Core == null || !candidate.Core.IsSimulating
                    || _askedToday.Contains(AskedKey(toolType, candidate.VillagerId)))
                {
                    continue;
                }

                float sqr = (candidate.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = candidate;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private void Finish(string reason)
        {
            AILog.Log(AILog.TOOL, Id, $"Inquiry ended ({reason})");
            _phase = Phase.None;
            _partner = null;
        }
    }
}
