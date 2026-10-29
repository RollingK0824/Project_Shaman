#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Minion;
using ProjectShaman.AI.Mock;
using ProjectShaman.AI.Interfaces;
using Object = UnityEngine.Object;

// Real prefab, real MinionSpawner.BeginDefense/Update, production movement and item UseActions.
// Only the factory's unrelated villager setup and wave timing are supplied by this fixture.
// No AI control/timer fields are forced, and no fake damage/control receivers are used.
public class SpawnedMinionItemTests
{
    private readonly List<Object> _created = new();
    private readonly Vector3 _origin = new(3000, 0, 3000);
    private NavMeshDataInstance _nav;
    private MinionSpawner _spawner;
    private AI_Minion _minion;
    private INetworkMovable _agent;
    private readonly List<LocalGameplayJudge> _pausedJudges = new();
    private GameObject _owner;
    private int _controls, _damageEvents, _consumed;
    private ItemControlEffect _lastControl;

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private GameObject Make(string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.position = position; _created.Add(go); return go;
    }
    private static ItemData Data(string name) => AssetDatabase.LoadAssetAtPath<ItemData>(
        "Assets/Project/Data/Items/" + name + "ItemData.asset");
    private void Note(string text)
    {
        Debug.Log("[MinionItems QA] " + text);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _controls = _damageEvents = _consumed = 0;
        foreach (var judge in Object.FindObjectsByType<LocalGameplayJudge>(FindObjectsSortMode.None))
            if (judge.isActiveAndEnabled) { judge.enabled = false; _pausedJudges.Add(judge); }
        // A separate runtime NavMesh keeps the user's scene and baked NavMesh untouched.
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
            shape = NavMeshBuildSourceShape.Box, size = new Vector3(90, .2f, 90),
            transform = Matrix4x4.TRS(_origin - Vector3.up * .1f, Quaternion.identity, Vector3.one), area = 0 } };
        var nav = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
            new Bounds(_origin, new Vector3(100, 10, 100)), Vector3.zero, Quaternion.identity);
        Assert.IsNotNull(nav); _created.Add(nav); _nav = NavMesh.AddNavMeshData(nav);
        Assert.IsTrue(NavMesh.SamplePosition(_origin, out _, 2, NavMesh.AllAreas));

        var floor = Make("QA floor", _origin - Vector3.up * .1f);
        floor.AddComponent<BoxCollider>().size = new Vector3(90, .2f, 90);
        _owner = Make("Item user (production PlayerHealth)", _origin - Vector3.forward * 2.5f);
        _owner.AddComponent<PlayerHealth>();
        _owner.AddComponent<LocalGameplayJudge>();
        // No PlayerGhostTarget until the particular test needs actual aggro.
        var objective = Make("QA objective", _origin + Vector3.forward * 32);
        objective.AddComponent<MockDefenseFlag>();
        var anchor = Make("QA spawn point", _origin);
        var factory = Make("QA factory", _origin).AddComponent<AIFactory>();
        typeof(AIFactory).GetProperty("SpawnHandler").SetValue(factory, new LocalSpawnHandler());
        var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MinionWaveConfig>(
            "Assets/Project/Data/AI/Minion/MinionWaveConfig.asset"));
        _created.Add(config);
        Set(config, "_firstWaveDelay", 0f); Set(config, "_minionsPerWave", 1);
        Set(config, "_maxWaveCount", 1); Set(config, "_spawnJitterRadius", 0f);
        Set(config, "_minPlayerDistance", 0f);
        var spawnerObject = Make("QA real MinionSpawner", _origin); spawnerObject.SetActive(false);
        _spawner = spawnerObject.AddComponent<MinionSpawner>();
        Set(_spawner, "_factory", factory); Set(_spawner, "_config", config);
        Set(_spawner, "_objective", objective.transform); Set(_spawner, "_spawnPoints", new[] { anchor.transform });
        Set(_spawner, "_minionPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/AI/Temp_Minion.prefab"));
        spawnerObject.SetActive(true); _spawner.BeginDefense(0f);
        for (int i = 0; i < 100 && _spawner.AliveCount == 0; i++) yield return null;
        Assert.AreEqual(1, _spawner.AliveCount, "Actual wave must spawn one minion");
        foreach (var candidate in Object.FindObjectsByType<AI_Minion>(FindObjectsSortMode.None))
            if (Vector3.Distance(candidate.transform.position, _origin) < 10) _minion = candidate;
        Assert.IsNotNull(_minion); _agent = _minion.GetComponent<INetworkMovable>();
        Assert.IsNotNull(_agent);
        Assert.IsTrue(_minion.GetComponent<AI_Status>().IsActive);
        Assert.AreEqual(MinionState.MoveToObjective, _minion.State);
        _minion.OnControlApplied += (_, effect) => { _controls++; _lastControl = effect; };
        _minion.GetComponent<AI_Status>().OnDamaged += (_, amount, source) => _damageEvents++;
        yield return new WaitForSeconds(.15f);
        Assert.Greater(_agent.CurrentSpeed, .05f, "Spawned minion must actually move");
        Note("SPAWN: MinionSpawner -> Initialize -> LocalSpawnHandler; active production movement, HP=" + _minion.GetComponent<AI_Status>().Health);
    }

    private bool Use(string item)
    {
        var data = Data(item); Assert.IsNotNull(data); Assert.IsNotNull(data.UseAction);
        Physics.SyncTransforms();
        var origin = _owner.transform.position + Vector3.up;
        return data.UseAction.Execute(new ItemUseContext(_owner, data, origin,
            _minion.transform.position + Vector3.up - origin, n => _consumed += n));
    }

    [UnityTest]
    public IEnumerator Salt_RealSpawn_UnsupportedSlowDoesNotDamageMinionOrPlayer()
    {
        _owner.transform.position = _minion.transform.position - Vector3.forward * 1.5f;
        var extra = Make("Second minion hitbox", _minion.transform.position);
        extra.transform.SetParent(_minion.transform, false); extra.transform.localPosition = Vector3.up;
        extra.AddComponent<SphereCollider>().radius = .25f;
        var otherPlayer = Make("Other player", _minion.transform.position + Vector3.right * .1f);
        var playerHP = otherPlayer.AddComponent<PlayerHealth>();
        var playerCollider = otherPlayer.AddComponent<CapsuleCollider>();
        playerCollider.center = Vector3.up; playerCollider.radius = .12f; playerCollider.height = 2; playerCollider.isTrigger = true;
        var hp = _minion.GetComponent<AI_Status>(); float before = hp.Health;
        Assert.IsTrue(Use("Salt")); yield return new WaitForSeconds(.2f);
        Assert.AreEqual(before, hp.Health);
        Assert.AreEqual(0, _damageEvents, "Salt now requests slow, never damage");
        Assert.AreEqual(playerHP.MaxHealth, playerHP.CurrentHealth);
        Assert.AreEqual(0, _controls);
        Note("SALT PASS: minion rejects slow; damage events=0; other player HP unchanged");
    }

    [UnityTest]
    public IEnumerator Bell_InterruptsActualAttack_StopsAndResumes()
    {
        _owner.transform.position = _minion.transform.position - Vector3.forward;
        _owner.AddComponent<PlayerGhostTarget>();
        for (int i = 0; i < 200 && _minion.State != MinionState.Attack; i++) yield return null;
        Assert.AreEqual(MinionState.Attack, _minion.State);
        float hp = _owner.GetComponent<PlayerHealth>().CurrentHealth;
        Assert.IsTrue(Use("Bell"));
        Assert.AreEqual(1, _controls); Assert.AreEqual(ItemControlKind.Stagger, _lastControl.Kind);
        Assert.AreEqual(MinionState.ChasePlayer, _minion.State); Assert.IsTrue(_agent.IsStopped);
        yield return new WaitForSeconds(.25f);
        Assert.AreEqual(hp, _owner.GetComponent<PlayerHealth>().CurrentHealth, "Interrupted attack must not hit");
        Assert.IsTrue(_agent.IsStopped);
        Object.Destroy(_owner.GetComponent<PlayerGhostTarget>());
        _owner.transform.position += Vector3.back * 20;
        yield return new WaitForSeconds(.55f);
        Assert.IsFalse(_agent.IsStopped); Assert.Greater(_agent.CurrentSpeed, .05f);
        Assert.AreEqual(1, _controls); Assert.AreEqual(0, _damageEvents);
        Note("BELL PASS: attack cancelled; 0.5s stagger; movement resumes; HP unchanged; control events=1");
    }

    [UnityTest]
    // Explicit contact injection tests the payload on a spawned minion, not aiming/flight accuracy.
    public IEnumerator BeanMugwort_RealProjectile_InjectedContactDamagesMinionOnce()
    {
        _owner.transform.position = _minion.transform.position - Vector3.forward * 2;
        float before = _minion.GetComponent<AI_Status>().Health;
        Assert.IsTrue(Use("BeanMugwort"));
        BeanMugwortProjectile projectile = null;
        foreach (var candidate in Object.FindObjectsByType<BeanMugwortProjectile>(FindObjectsSortMode.None))
            if (Vector3.Distance(candidate.transform.position, _owner.transform.position) < 5) projectile = candidate;
        Assert.IsNotNull(projectile);
        foreach (var collider in _minion.GetComponentsInChildren<Collider>()) projectile.SendMessage("OnTriggerEnter", collider);
        Assert.IsNotNull(_minion);
        Assert.AreEqual(0, _controls, "Bean no longer requests slow");
        Assert.AreEqual(Mathf.Max(0, before - 10), _minion.GetComponent<AI_Status>().Health);
        Assert.AreEqual(1, _damageEvents);
        Note("BEAN PASS: injected real projectile contact; one damage event; damage=10; no slow");
        yield return null;
    }

    [UnityTest]
    // Gong = fear on a minion (10/25): it drops the chase and flies away from where the gong rang, then comes back
    public IEnumerator Gong_AssetFlags_FearFleesThenAllowsReacquisition()
    {
        var data = Data("Gong");
        var senses = new SerializedObject(data.UseAction).FindProperty("_senses").intValue;
        Assert.AreEqual((int)(ItemSense.Vision | ItemSense.Hearing), senses);
        _owner.transform.position = _minion.transform.position - Vector3.forward * 4;
        _owner.AddComponent<PlayerGhostTarget>();
        for (int i = 0; i < 100 && _minion.State != MinionState.ChasePlayer; i++) yield return null;
        Assert.AreEqual(MinionState.ChasePlayer, _minion.State);
        Assert.IsTrue(Use("Gong")); Assert.AreEqual(1, _controls);
        Assert.AreEqual(ItemSense.Vision | ItemSense.Hearing, _lastControl.Senses);
        yield return null; yield return null;
        Assert.AreEqual(MinionState.MoveToObjective, _minion.State);
        Vector3 gongAt = _owner.transform.position;
        float before = Vector3.Distance(_minion.transform.position, gongAt);
        yield return new WaitForSeconds(.8f);
        Assert.Greater(Vector3.Distance(_minion.transform.position, gongAt), before, "A feared minion flies away from the gong");
        float end = Time.time + 1.9f;
        while (Time.time < end)
        {
            _owner.transform.position = _minion.transform.position + Vector3.right * 2;
            Assert.AreEqual(MinionState.MoveToObjective, _minion.State, "No reacquisition while senses blocked");
            yield return null;
        }
        for (int i = 0; i < 120 && _minion.State == MinionState.MoveToObjective; i++)
        { _owner.transform.position = _minion.transform.position + Vector3.right * 2; yield return null; }
        Assert.AreEqual(MinionState.ChasePlayer, _minion.State);
        Assert.AreEqual(1, _controls); Assert.AreEqual(0, _damageEvents);
        Note("GONG PASS: actual asset flags=3; chase dropped; flees from the gong; aggro blocked during fear; reacquires after 3s; damage=0");
    }

    [UnityTest]
    public IEnumerator DeadUser_CannotExecuteAnyAttack()
    {
        _owner.GetComponent<PlayerHealth>().ReceiveDamage(10000);
        foreach (string item in new[] { "Salt", "Bell", "BeanMugwort", "Gong" }) Assert.IsFalse(Use(item), item);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual(0, _damageEvents); Assert.AreEqual(0, _controls); Assert.AreEqual(0, _consumed);
        Note("DEAD USER PASS: all four actions rejected");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_spawner != null) _spawner.StopDefense("QA finished");
        foreach (var volume in Object.FindObjectsByType<DamageTrigger>(FindObjectsSortMode.None))
            if (Vector3.Distance(volume.transform.position, _origin) < 100) Object.Destroy(volume.gameObject);
        foreach (var projectile in Object.FindObjectsByType<BeanMugwortProjectile>(FindObjectsSortMode.None))
            if (Vector3.Distance(projectile.transform.position, _origin) < 100) Object.Destroy(projectile.gameObject);
        for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null && !(_created[i] is NavMeshData)) Object.Destroy(_created[i]);
        yield return null;
        _nav.Remove();
        foreach (var obj in _created) if (obj is NavMeshData) Object.Destroy(obj);
        _created.Clear(); _minion = null; _spawner = null;
        foreach (var judge in _pausedJudges) if (judge != null) judge.enabled = true;
        _pausedJudges.Clear();
    }

}
#endif
