using Mirror;
using UnityEngine;
using ProjectShaman.AI;

namespace ProjectShaman.Network.AI
{
    // The server picks the model before spawning; the pick rides in the spawn message so every client
    // (late joiners included) builds the same one
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_ModelVisual))]
    public class Net_ModelVisual : NetworkBehaviour
    {
        private AI_ModelVisual _visual;

        [SyncVar] private int _modelIndex = AI_ModelVisual.NONE;

        private void Awake()
        {
            _visual = GetComponent<AI_ModelVisual>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _modelIndex = _visual.ModelIndex;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer && _modelIndex != AI_ModelVisual.NONE)
            {
                _visual.Apply(_modelIndex);
            }
        }
    }
}
