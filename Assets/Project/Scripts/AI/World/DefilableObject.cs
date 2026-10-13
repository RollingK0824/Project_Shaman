using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class DefilableObject : MonoBehaviour, IGhostDefilable
    {
        [SerializeField] private GhostTargetKind _kind = GhostTargetKind.Well;

        [Header("Runtime (server only)")]
        [SerializeField] private bool _isDefiled;

        public event Action<DefilableObject> OnDefiled;
        public event Action<DefilableObject> OnRestored;

        public GhostTargetKind Kind => _kind;
        public bool IsDefiled => _isDefiled;

        private void OnEnable()
        {
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            GhostTargetRegistry.Unregister(this);
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            return context.Verb == GhostActionVerb.Defile && !_isDefiled;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        public void ReceiveDefile(in GhostActionContext context)
        {
            _isDefiled = true;
            AILog.Log(AILog.GHOST, context.GhostId, $"Defiled {_kind} {name}");
            OnDefiled?.Invoke(this);
        }

        public void Restore()
        {
            if (!_isDefiled)
            {
                return;
            }

            _isDefiled = false;
            AILog.Log(AILog.GHOST, name, $"{_kind} restored");
            OnRestored?.Invoke(this);
        }
    }
}
