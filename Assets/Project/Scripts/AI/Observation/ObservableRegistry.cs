using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI.Observation
{
    // Something a villager can notice while it lasts: a symptom, a corrupted routine, an anxious mutter.
    // Night events never register here; they leave traces that are discovered by day (GhostTrace).
    public sealed class Observable
    {
        public string ActorId;
        public Transform Actor;
        public string VerbId;
        public string ObjectId;
        public bool IsAnomalous;
        public float ExpiresAt;
        public Func<bool> IsVisibleNow;

        public Vector3 Position => Actor != null ? Actor.position : Vector3.zero;
        public bool IsLive => Actor != null && Time.time < ExpiresAt && (IsVisibleNow == null || IsVisibleNow());

        public override string ToString()
        {
            return $"{ActorId} {VerbId}{(string.IsNullOrEmpty(ObjectId) ? string.Empty : $"({ObjectId})")}";
        }
    }

    public static class ObservableRegistry
    {
        private static readonly List<Observable> _all = new List<Observable>();

        public static IReadOnlyList<Observable> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _all.Clear();
        }

        public static Observable Register(string actorId, Transform actor, string verbId, string objectId, float seconds, bool isAnomalous = true, Func<bool> isVisibleNow = null)
        {
            if (string.IsNullOrEmpty(actorId) || actor == null || string.IsNullOrEmpty(verbId) || seconds <= 0f)
            {
                return null;
            }

            Observable observable = new Observable
            {
                ActorId = actorId,
                Actor = actor,
                VerbId = verbId,
                ObjectId = objectId,
                IsAnomalous = isAnomalous,
                ExpiresAt = Time.time + seconds,
                IsVisibleNow = isVisibleNow
            };

            _all.Add(observable);
            return observable;
        }

        public static void RemoveActor(string actorId)
        {
            _all.RemoveAll(o => o.ActorId == actorId);
        }

        public static void Clear()
        {
            _all.Clear();
        }

        public static void Prune()
        {
            float now = Time.time;
            _all.RemoveAll(o => o.Actor == null || now >= o.ExpiresAt);
        }
    }
}
