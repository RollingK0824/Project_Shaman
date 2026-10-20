using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Trace
{
    public static class GhostTraceRegistry
    {
        private static readonly List<GhostTrace> _traces = new List<GhostTrace>();

        public static IReadOnlyList<GhostTrace> Traces => _traces;

        public static event Action<GhostTrace> OnTraceAdded;
        public static event Action<GhostTrace, TraceResolveReason> OnTraceResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _traces.Clear();
            OnTraceAdded = null;
            OnTraceResolved = null;
        }

        public static void Register(GhostTrace trace)
        {
            if (trace == null || _traces.Contains(trace))
            {
                return;
            }

            _traces.Add(trace);
            OnTraceAdded?.Invoke(trace);
        }

        public static void Unregister(GhostTrace trace)
        {
            _traces.Remove(trace);
        }

        public static void NotifyResolved(GhostTrace trace, TraceResolveReason reason)
        {
            OnTraceResolved?.Invoke(trace, reason);
        }

        public static int CountActive(GhostTraceKind kind)
        {
            int count = 0;

            foreach (GhostTrace trace in _traces)
            {
                if (trace != null && trace.IsActive && trace.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        public static GhostTrace FindOldestActive(GhostTraceKind kind, GhostTrace except)
        {
            GhostTrace oldest = null;

            foreach (GhostTrace trace in _traces)
            {
                if (trace == null || trace == except || !trace.IsActive || trace.Kind != kind)
                {
                    continue;
                }

                if (oldest == null || trace.Serial < oldest.Serial)
                {
                    oldest = trace;
                }
            }

            return oldest;
        }

        public static void CollectActive(string bundleKey, List<GhostTrace> results)
        {
            results.Clear();

            foreach (GhostTrace trace in _traces)
            {
                if (trace != null && trace.IsActive && trace.BundleKey == bundleKey)
                {
                    results.Add(trace);
                }
            }
        }
    }
}
