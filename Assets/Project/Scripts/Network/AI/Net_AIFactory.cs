using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.Network.AI
{
    [RequireComponent(typeof(NetworkIdentity), typeof(AIFactory))]
    public partial class Net_AIFactory : NetworkBehaviour, ISpawnHandler
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
            AIEvents.OnVillagerDied += HandleVillagerDied;
            AIEvents.OnVillagerDeathKnown += HandleDeathKnown;
            AIEvents.OnVillagerExorcised += HandleExorcised;
            AIEvents.OnVillagerDayPlanned += HandleDayPlanned;
            AIEvents.OnVillagerSlotRested += HandleSlotRested;
            _factory.CreateVillagers(this);
            ServerBindQuests();
        }

        // The lose check counts every death, known to the village or not (only the vote lists wait for the body)
        private void HandleVillagerDied(string villagerId, string reason)
        {
            RpcVillagerDied(villagerId);

            NetGM gameManager = FindFirstObjectByType<NetGM>();
            if (gameManager == null)
            {
                AILog.Log(AILog.STUB, villagerId, "NetGM not in scene, death report skipped");
                return;
            }

            gameManager.ServerReportNpcDied();
            AILog.Log(AILog.NET, villagerId, "Death reported to NetGM");
        }

        public override void OnStopServer()
        {
            AIEvents.OnVillagerDied -= HandleVillagerDied;
            AIEvents.OnVillagerDeathKnown -= HandleDeathKnown;
            AIEvents.OnVillagerExorcised -= HandleExorcised;
            AIEvents.OnVillagerDayPlanned -= HandleDayPlanned;
            AIEvents.OnVillagerSlotRested -= HandleSlotRested;
            ServerUnbindQuests();
            AILog.Log(AILog.NET, "Server stopped → AIFactory.ResetFactory");
            _factory.ResetFactory();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ClientBindQuests();
            AILog.Log(AILog.NET, $"Net_AIFactory OnStartClient (isServer={isServer}, ready={NetworkClient.ready})");
        }

        public override void OnStopClient()
        {
            _factory.Registry?.Clear();
            ClientUnbindQuests();
            base.OnStopClient();
        }

        // The village learns of a death (body cleared, or found dead when voted out): it leaves the vote lists
        private void HandleDeathKnown(string villagerId)
        {
            RpcVillagerDeathKnown(villagerId);
        }

        private void HandleExorcised(string villagerId)
        {
            RpcVillagerExorcised(villagerId);
        }

        // Every villager's day, the dead not yet known included: their own objects are gone, so the hours come through here
        private void HandleDayPlanned(string villagerId, int day, int[] blockHours, int leaveHour, int homeHour, int bedHour)
        {
            RpcVillagerDayPlanned(villagerId, day, blockHours, leaveHour, homeHour, bedHour);
        }

        [ClientRpc]
        private void RpcVillagerDayPlanned(string villagerId, int day, int[] blockHours, int leaveHour, int homeHour, int bedHour)
        {
            if (!isServer)
            {
                _factory.Registry?.SetDayHours(villagerId, day, blockHours, leaveHour, homeHour, bedHour);
            }
        }

        private void HandleSlotRested(string villagerId, int day, int slot, bool rested, string restPlaceId)
        {
            RpcVillagerSlotRested(villagerId, day, slot, rested, restPlaceId);
        }

        [ClientRpc]
        private void RpcVillagerSlotRested(string villagerId, int day, int slot, bool rested, string restPlaceId)
        {
            if (!isServer)
            {
                _factory.Registry?.SetSlotRested(villagerId, day, slot, rested, restPlaceId);
            }
        }

        [ClientRpc]
        private void RpcVillagerExorcised(string villagerId)
        {
            if (!isServer)
            {
                _factory.Registry?.MarkExorcised(villagerId);
            }
        }

        [ClientRpc]
        private void RpcVillagerDeathKnown(string villagerId)
        {
            if (!isServer)
            {
                _factory.Registry?.MarkDeathKnown(villagerId);
            }
        }

        [ClientRpc]
        private void RpcVillagerDied(string villagerId)
        {
            if (!isServer)
            {
                _factory.Registry?.MarkDead(villagerId);
            }
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

        [Server]
        public void Despawn(GameObject instance)
        {
            if (instance == null || !NetworkServer.active)
            {
                return;
            }

            AILog.Log(AILog.NET, $"NetworkServer.Destroy: {instance.name}");
            NetworkServer.Destroy(instance);
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
