using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI
{
    // Puts one of the model prefabs under the empty Visual child. The server picks before spawning;
    // Net_ModelVisual carries the pick to clients, which build the same model on spawn
    public class AI_ModelVisual : MonoBehaviour
    {
        public const int NONE = -1;

        [SerializeField] private Transform _visualRoot;
        [SerializeField] private GameObject[] _modelPrefabs;

        [Header("Runtime")]
        [SerializeField] private int _modelIndex = NONE;

        private GameObject _model;

        public int ModelCount => _modelPrefabs != null ? _modelPrefabs.Length : 0;
        public int ModelIndex => _modelIndex;

        // Any number works: it wraps around the list, so a fixed slot keeps the same model
        public void Choose(int slot)
        {
            if (ModelCount == 0)
            {
                AILog.Warn(AILog.CORE, $"{name}: no model prefabs, Visual stays empty");
                return;
            }

            Apply(((slot % ModelCount) + ModelCount) % ModelCount);
        }

        public void Apply(int index)
        {
            if (index == _modelIndex && _model != null)
            {
                return;
            }

            if (index < 0 || index >= ModelCount || _modelPrefabs[index] == null)
            {
                AILog.Warn(AILog.CORE, $"{name}: model {index} missing (count={ModelCount})");
                return;
            }

            if (_model != null)
            {
                Destroy(_model);
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            _model = Instantiate(_modelPrefabs[index], root, false);
            _modelIndex = index;
            AILog.Log(AILog.CORE, name, $"Model {index}: {_modelPrefabs[index].name}");
        }
    }
}
