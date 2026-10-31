using System.Collections;
using Mirror;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Mock;
using UnityEngine;

// Explicitly installed only in PlayerPrototype/Gameplay; never auto-loads in production scenes.
[DefaultExecutionOrder(-30000)]
public sealed class PlayerGameplaySceneTestBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private PlayerSpawnArea _spawnArea;
    [SerializeField] private AIFactory _factory;
    [SerializeField] private TalismanItemData _testTalisman;
    [SerializeField] private bool _giveTrackingSample = true;
    public GameObject LocalPlayer { get; private set; }
    private bool _valid;
    private TimeManager _localClock;
    private double _localElapsed;
    private void Update()
    {
        if (!_valid || _localClock == null || NetworkClient.active || NetworkServer.active) return;
        // Offline Mirror has no advancing client timeline. Drive Core through its public
        // start-time API in this test scene only, without changing any network clock state.
        _localElapsed += Time.deltaTime;
        _localClock.SetCycleStart(NetworkTime.time - _localElapsed);
    }
    private void Awake()
    {
        _valid = gameObject.scene.path == "Assets/Project/Scenes/PlayerPrototype/Gameplay.unity";
        if (!_valid || NetworkClient.active || NetworkServer.active) { enabled=false; return; }
        // Runtime-only scene copies. Shared prefabs, transports and network scripts stay unchanged.
        foreach(var root in gameObject.scene.GetRootGameObjects())
        {
            foreach(var manager in root.GetComponentsInChildren<NetworkManager>(true)) manager.gameObject.SetActive(false);
            DisableNetworkAdapters(root);
        }
    }
    private IEnumerator Start()
    {
        if (!_valid || _playerPrefab==null) yield break;
        // Mirror's scene postprocessor hides scene identities until a server spawns them.
        // Restore only this offline test scene's instances; leave the network start gate dormant.
        foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var identity in root.GetComponentsInChildren<NetworkIdentity>(true))
                if(identity.GetComponent<NetGameStartGate>()==null)
                    identity.gameObject.SetActive(true);
        yield return null; // All data, map and UI Awake callbacks must finish first.
        if (NetworkClient.active || NetworkServer.active) yield break;
        Vector3 position=transform.position;Quaternion rotation=transform.rotation;
        if (_spawnArea!=null) _spawnArea.GetSpawnPose(0,1,out position,out rotation);
        if(FindFirstObjectByType<LocalGameplayJudge>()==null) gameObject.AddComponent<LocalGameplayJudge>();
        LocalPlayer=Instantiate(_playerPrefab,position,rotation);
        LocalPlayer.name="Local Player (Gameplay Test)";
        DisableNetworkAdapters(LocalPlayer);
        // Existing Core APIs start the local clock and real villagers without opening a server socket.
        var clock=TimeManager.Existing;
        if(clock!=null&&!clock.IsRunning) clock.SetCycleStart(NetworkTime.time);
        _localClock=clock;
        _localElapsed=clock!=null?clock.Elapsed:0;
        if(_factory!=null && _factory.SpawnedVillagers.Count==0) _factory.CreateVillagers(new LocalSpawnHandler());
        if(_giveTrackingSample && _testTalisman!=null)
        {
            var inventory=LocalPlayer.GetComponent<PlayerInventory>();
            var collection=LocalPlayer.GetComponent<PlayerTalismanCollection>();
            if(inventory!=null&&collection!=null&&inventory.TryAddItem(_testTalisman,3))
            {
                var draft=collection.CreateDraft(_testTalisman);
                if(draft!=null)
                {
                    draft.BeginStroke(new Vector2(.3f,.2f),false,3);draft.AppendPoint(new Vector2(.5f,.8f));draft.AppendPoint(new Vector2(.7f,.2f));draft.EndStroke();
                    if(collection.Save(_testTalisman,draft,"추적 시험 부적","track_villager"))
                    {
                        var saved=collection.GetSelected(_testTalisman);
                        if(saved!=null) inventory.AssignTalismanQuickSlot(0,saved.Id);
                    }
                }
            }
        }
        Debug.Log("[PlayerGameplaySceneTest] Offline player, camera, clock and villagers ready. Select slot 1 to test tracking. No Host required.",this);
    }

    private static void DisableNetworkAdapters(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<NetworkBehaviour>(true))
        {
            // PlayerController also derives from NetworkBehaviour, but owns the actual
            // offline locomotion loop. Its existing network guard already supports local play.
            if (component is PlayerController) continue;
            component.enabled = false;
        }
    }
}
