using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Ghost
{
    public class AI_Possession : MonoBehaviour
    {
        [Header("Runtime (server only)")]
        [SerializeField] private string _ghostId;
        [SerializeField] private int _seed;
        [SerializeField] private float _yin;
        [SerializeField] private float _pendingYin;
        [SerializeField] private List<string> _planDebug = new List<string>();

        public string GhostId => _ghostId;
        public int Seed => _seed;
        public float Yin => _yin;
        public float PendingYin => _pendingYin;
        public CorruptionPlan TodayPlan { get; private set; }

        public void Initialize(string ghostId, int seed, float initialYin)
        {
            _ghostId = ghostId;
            _seed = seed;
            _yin = initialYin;
            _pendingYin = 0f;
            AILog.Log(AILog.GHOST, name, $"Possessed by {ghostId} (seed={seed}, yin={initialYin})");
        }

        public void AddPendingYin(float amount, string reason)
        {
            _pendingYin += amount;
            AILog.Log(AILog.GHOST, _ghostId, $"Pending yin +{amount} ({reason}) → {_pendingYin}");
        }

        public void SpendYin(float amount, string reason)
        {
            _yin = Mathf.Max(0f, _yin - amount);
            AILog.Log(AILog.GHOST, _ghostId, $"Yin -{amount} ({reason}) → {_yin}");
        }

        public void ApplyMorningYin(float minimumYin)
        {
            float before = _yin;
            _yin = Mathf.Max(_yin + _pendingYin, minimumYin);
            AILog.Log(AILog.GHOST, _ghostId, $"Morning yin {before} + pending {_pendingYin} → {_yin} (min {minimumYin})");
            _pendingYin = 0f;
        }

        public void SetPlan(CorruptionPlan plan)
        {
            TodayPlan = plan;
            RefreshDebug();
        }

        public void RefreshDebug()
        {
            if (TodayPlan != null)
            {
                TodayPlan.WriteDebug(_planDebug);
            }
        }
    }
}
