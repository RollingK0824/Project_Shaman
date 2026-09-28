using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Mock
{
    [CreateAssetMenu(fileName = "MockAIDataProvider", menuName = "ProjectShaman/AI/Mock AI Data Provider")]
    public class MockAIDataProvider : ScriptableObject, IAIDataProvider
    {
        [SerializeField] private List<NameEntry> _names = new List<NameEntry>();
        [SerializeField] private List<JobEntry> _jobs = new List<JobEntry>();

        public string SourceName => $"Mock({name})";
        public IReadOnlyList<NameEntry> Names => _names;
        public IReadOnlyList<JobEntry> Jobs => _jobs;
    }
}
