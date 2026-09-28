using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AIFactory))]
    public class Net_AIFactory : NetworkBehaviour, ISpawnHandler
    {
        private AIFactory _factory;

        public string HandlerName => "Mirror";
        public bool CanSpawn => NetworkServer.active;

        void Awake()
        {
            _factory = GetComponent<AIFactory>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            AILog.Log(AILog.NET, "Server started → AIFactory.CreateVillagers");
            ValidateSpawnPrefabRegistration();
            _factory.CreateVillagers(this);
        }

        public override void OnStopServer()
        {
            AILog.Log(AILog.NET, "Server stopped → AIFactory.ResetFactory");
            _factory.ResetFactory();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            AILog.Log(AILog.NET, $"Net_AIFactory OnStartClient (isServer={isServer}, ready={NetworkClient.ready})");
        }

        [Server]
        public void Spawn(GameObject instance)
        {
            if (!NetworkServer.active)
            {
                AILog.Error(AILog.NET, $"Spawn called while server inactive: {instance.name}");
                return;
            }

            NetworkServer.Spawn(instance);
            AILog.Log(AILog.NET, $"NetworkServer.Spawn done: {instance.name}");
        }

        private void ValidateSpawnPrefabRegistration()
        {
            NetworkManager manager = NetworkManager.singleton;
            GameObject prefab = _factory.VillagerPrefab;

            if (manager == null || prefab == null)
            {
                return;
            }

            if (!manager.spawnPrefabs.Contains(prefab))
            {
                AILog.Warn(AILog.NET, $"{prefab.name} is not in NetworkManager.spawnPrefabs, clients cannot spawn it");
            }
        }
    }
}
