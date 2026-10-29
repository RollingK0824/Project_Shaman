using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI;
using ProjectShaman.AI.Minion;
using UnityEngine;
using UnityEngine.TestTools;

public class SaltTargetingTests
{
    private readonly List<GameObject> _objects = new();
    private readonly List<LocalGameplayJudge> _pausedJudges = new();
    private readonly Vector3 _origin = new(1000, 1000, 1000);
    private GameObject _owner;
    private SaltUseAction _action;
    private ItemData _data;
    private int _requests, _consumed;

    [SetUp]
    public void SetUp()
    {
        _requests = 0;
        _consumed = 0;
        // This fixture resolves its own DamageRequested events. Isolate scene judges
        // so running in a populated prototype scene cannot apply the same request twice.
        foreach (var judge in Object.FindObjectsByType<LocalGameplayJudge>(FindObjectsSortMode.None))
            if (judge.isActiveAndEnabled) { judge.enabled = false; _pausedJudges.Add(judge); }
        _owner = Create("Salt user", 0);
        _action = ScriptableObject.CreateInstance<SaltUseAction>();
        _data = ScriptableObject.CreateInstance<ItemData>();
        Set("_consumeOnSuccessfulUse", true);
        PlayerEvents.DamageRequested += Damage;
    }

    private void Set(string name, object value) => typeof(SaltUseAction)
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_action, value);

    private GameObject Create(string name, float forward)
    {
        var go = new GameObject(name);
        go.transform.position = _origin + Vector3.forward * forward;
        _objects.Add(go);
        return go;
    }

    private Collider Hitbox(GameObject parent)
    {
        var child = Create("Child hitbox", 0);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = Vector3.zero;
        return child.AddComponent<BoxCollider>();
    }

    private GameObject Ghost(bool isMinion = false)
    {
        var ghost = Create("Manifested ghost", 1);
        ghost.SetActive(false);
        if (isMinion) ghost.AddComponent<AI_FlyingMovement>(); // Current minion requires INetworkMovable even in contract tests.
        Behaviour identity = isMinion ? (Behaviour)ghost.AddComponent<AI_Minion>() : ghost.AddComponent<AI_GhostEntity>();
        // The test exercises identity and hit contracts, not AI simulation/NavMesh.
        foreach (var behaviour in ghost.GetComponents<Behaviour>())
            if (behaviour != identity) behaviour.enabled = false;
        ghost.SetActive(true);
        return ghost;
    }

    private void Damage(IDamageable target, float amount, GameObject source)
    {
        if (source != _owner) return;
        _requests++;
        target.ReceiveDamage(amount, source);
    }

    private bool Spray()
    {
        Physics.SyncTransforms();
        return _action.Execute(new ItemUseContext(_owner, _data, _origin, Vector3.forward,
            amount => _consumed += amount));
    }

    [TearDown]
    public void TearDown()
    {
        PlayerEvents.DamageRequested -= Damage;
        foreach (var projectile in Object.FindObjectsByType<BeanMugwortProjectile>(FindObjectsSortMode.None))
            if (Vector3.Distance(projectile.transform.position, _origin) < 10) Object.DestroyImmediate(projectile.gameObject);
        foreach (var burst in Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None))
            if (Vector3.Distance(burst.transform.position, _origin) < 10) Object.DestroyImmediate(burst.gameObject);
        foreach (var volume in Object.FindObjectsByType<DamageTrigger>(FindObjectsSortMode.None))
            if (Vector3.Distance(volume.transform.position, _origin) < 10)
                Object.DestroyImmediate(volume.gameObject);
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
        Object.DestroyImmediate(_action);
        Object.DestroyImmediate(_data);
        foreach (var judge in _pausedJudges) if (judge != null) judge.enabled = true;
        _pausedJudges.Clear();
    }

    [Test]
    public void OtherPlayerIsNotDamagedOrCountedAsHit()
    {
        var player = Create("Other player", 1);
        var health = player.AddComponent<PlayerHealth>();
        Hitbox(player); Hitbox(player);
        float before = health.CurrentHealth;
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(before, health.CurrentHealth);
        Assert.AreEqual(0, _consumed);
    }

    [Test]
    public void OrdinaryDamageableAndLegacyNpcAreExcluded()
    {
        var npc = Create("Ordinary NPC", 1);
        var receiver = npc.AddComponent<SaltTestReceiver>();
        Hitbox(npc);
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0, receiver.Reactions);
        Assert.AreEqual(0, _consumed);
    }

    [TestCase(false)]
    public void GhostReceivesSlowOnceAcrossChildColliders(bool isMinion)
    {
        var ghost = Ghost(isMinion);
        var receiver = ghost.AddComponent<SaltTestReceiver>();
        var first = Hitbox(ghost); var second = Hitbox(ghost);
        Assert.IsTrue(Spray());
        Assert.AreEqual(1, receiver.Controls, "Initial overlap must resolve the ghost slow");
        var volume = Object.FindFirstObjectByType<DamageTrigger>();
        volume.SendMessage("OnTriggerEnter", first);
        volume.SendMessage("OnTriggerExit", first);
        volume.SendMessage("OnTriggerEnter", second);
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0f, receiver.Damage);
        Assert.AreEqual(1, receiver.Controls);
        Assert.AreEqual(ItemControlKind.Slow, receiver.LastControl.Kind);
        Assert.AreEqual(.6f, receiver.LastControl.SpeedMultiplier);
        Assert.AreEqual(3f, receiver.LastControl.Duration);
        Assert.AreEqual(0, receiver.Reactions, "Common and legacy must not both apply");
        Assert.AreEqual(1, _consumed);
    }

    [TestCase(false)]
    public void LegacyGhostReactionIsStillSupported(bool isMinion)
    {
        var ghost = Ghost(isMinion);
        var receiver = ghost.AddComponent<SaltTestLegacyReceiver>();
        Hitbox(ghost);
        Assert.IsTrue(Spray());
        Assert.AreEqual(1, receiver.Reactions);
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(1, _consumed);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisabledGhostAndDeadGhostCannotBeHit(bool isMinion)
    {
        var ghost = Ghost(isMinion);
        var receiver = ghost.AddComponent<SaltTestReceiver>();
        Hitbox(ghost);
        Behaviour identity = isMinion ? (Behaviour)ghost.GetComponent<AI_Minion>() : ghost.GetComponent<AI_GhostEntity>();
        identity.enabled = false;
        Assert.IsTrue(Spray());
        identity.enabled = true;
        receiver.Dead = true;
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0, _consumed);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlayerHealthTakesPrecedenceOverGhostIdentity(bool isMinion)
    {
        var ghost = Ghost(isMinion);
        var health = ghost.AddComponent<PlayerHealth>();
        Hitbox(ghost);
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }

    [Test]
    public void OtherAttacksCanStillDamagePlayers()
    {
        var player = Create("Other player", 1);
        var health = player.AddComponent<PlayerHealth>();
        var hitbox = Hitbox(player);
        float before = health.CurrentHealth;
        var attack = new ItemAttackSession(_owner, 7, default, ItemReactionFallback.None, ~0, 1);
        Assert.IsTrue(attack.TryHit(hitbox));
        Assert.AreEqual(before - 7, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator LateTriggerContactsAlsoRejectPlayersAndAcceptGhostsOnce()
        => LateTriggerContacts(false);

    private IEnumerator LateTriggerContacts(bool isMinion)
    {
        Set("_contactDuration", 2f);
        var player = Create("Entering player", 8);
        var health = player.AddComponent<PlayerHealth>(); Hitbox(player);
        var ghost = Ghost(isMinion); ghost.transform.position = _origin + Vector3.forward * 8;
        var receiver = ghost.AddComponent<SaltTestReceiver>(); Hitbox(ghost); Hitbox(ghost);
        float before = health.CurrentHealth;
        Assert.IsTrue(Spray()); Assert.AreEqual(0, _requests);
        player.transform.position = _origin + Vector3.forward;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(before, health.CurrentHealth);
        ghost.transform.position = _origin + Vector3.forward * 2;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0f, receiver.Damage);
        Assert.AreEqual(1, receiver.Controls);
        Assert.AreEqual(1, _consumed);
    }

#if UNITY_EDITOR
    private BeanMugwortProjectile ThrowBean()
    {
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/BeanMugwortItemData.asset");
        Assert.IsNotNull(data);
        Assert.IsTrue(data.UseAction.Execute(new ItemUseContext(_owner, data, _origin, Vector3.forward,
            amount => _consumed += amount)));
        foreach (var projectile in Object.FindObjectsByType<BeanMugwortProjectile>(FindObjectsSortMode.None))
            if (Vector3.Distance(projectile.transform.position, _origin) < 10) return projectile;
        Assert.Fail("Production use action must spawn its projectile");
        return null;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BeanProjectileDamagesGhostOnceWithoutSlow(bool isMinion)
    {
        var ghost = Ghost(isMinion);
        var receiver = ghost.AddComponent<SaltTestReceiver>();
        var first = Hitbox(ghost); var second = Hitbox(ghost);
        var projectile = ThrowBean();
        projectile.SendMessage("OnTriggerEnter", first);
        projectile.SendMessage("OnTriggerEnter", second);
        Assert.AreEqual(1, _requests);
        Assert.AreEqual(10f, receiver.Damage);
        Assert.AreEqual(0, receiver.Controls);
        Assert.AreEqual(0, receiver.Reactions);
        Assert.AreEqual(1, _consumed);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BeanProjectileNeverDamagesPlayerOrOrdinaryNpc(bool player)
    {
        var target = Create("Non-ghost target", 1);
        if (player) target.AddComponent<PlayerHealth>();
        var receiver = target.AddComponent<SaltTestReceiver>();
        var collider = Hitbox(target);
        ThrowBean().SendMessage("OnTriggerEnter", collider);
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0, receiver.Damage);
        Assert.AreEqual(0, receiver.Controls);
        Assert.AreEqual(0, receiver.Reactions);
    }

    [Test]
    public void BeanProjectilePreservesLegacyRepelForGhostWithoutHp()
    {
        var ghost = Ghost();
        var receiver = ghost.AddComponent<BeanTestRepelReceiver>();
        ThrowBean().SendMessage("OnTriggerEnter", Hitbox(ghost));
        Assert.AreEqual(1, receiver.Reactions);
        Assert.AreEqual(0, _requests);
    }

    [Test]
    public void VisualBeanProjectileCannotApplyDamageOrSlow()
    {
        var ghost = Ghost();
        var receiver = ghost.AddComponent<SaltTestReceiver>();
        var projectile = ThrowBean();
        projectile.InitializeVisual(_owner, Vector3.forward, 0);
        projectile.SendMessage("OnTriggerEnter", Hitbox(ghost));
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0, receiver.Damage);
        Assert.AreEqual(0, receiver.Controls);
    }

    [Test]
    public void DeniedAuthorityCannotUseSaltOrBean()
    {
        _owner.AddComponent<ItemAttackAuthority>().SetAuthorityCheck(() => false);
        Assert.IsFalse(Spray());
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/BeanMugwortItemData.asset");
        Assert.IsFalse(data.UseAction.Execute(new ItemUseContext(_owner, data, _origin, Vector3.forward,
            amount => _consumed += amount)));
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(0, _consumed);
    }

    [Test]
    public void ActualMinionPrefabCurrentlyRejectsSlowWithoutDamageOrHitVfx()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/AI/Temp_Minion.prefab");
        Assert.IsNotNull(prefab);
        var parent = Create("Inactive test parent", 0);
        parent.SetActive(false);
        var minion = Object.Instantiate(prefab, parent.transform);
        _objects.Add(minion);
        minion.transform.position = _origin + Vector3.forward;
        // Use the real prefab's receiver/status. Network and NavMesh simulation are not under test.
        foreach (var behaviour in minion.GetComponentsInChildren<Behaviour>(true))
            behaviour.enabled = behaviour is AI_Minion || behaviour is AI_Status || behaviour is AI_DamageReceiver;
        parent.SetActive(true);
        var status = minion.GetComponent<AI_Status>();
        Assert.IsNotNull(minion.GetComponent<AI_DamageReceiver>());
        var presentation = _owner.AddComponent<PlayerSaltHitPresentation>();
        int confirmedHits = 0;
        presentation.ConfirmedHit += (_, _, _, _) => confirmedHits++;
        status.SetSimulationActive(false);
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, confirmedHits, "Ignored AI damage must not produce hit powder");
        foreach(var volume in Object.FindObjectsByType<DamageTrigger>(FindObjectsSortMode.None))
            if (Vector3.Distance(volume.transform.position, _origin) < 10) Object.DestroyImmediate(volume.gameObject);
        _requests = _consumed = 0;
        status.SetSimulationActive(true);
        float before = status.Health;
        Assert.Greater(before, 0f);
        Assert.IsTrue(Spray());
        Assert.AreEqual(0, _requests);
        Assert.AreEqual(before, status.Health);
        Assert.AreEqual(0, confirmedHits, "Rejected slow must not produce a confirmed hit");
        Assert.AreEqual(0, _consumed);

        // Same real receiver now takes the former salt damage from the bean projectile.
        ThrowBean().SendMessage("OnTriggerEnter", minion.GetComponentInChildren<Collider>());
        Assert.AreEqual(1, _requests);
        Assert.AreEqual(Mathf.Max(0f, before - 10f), status.Health);
        Assert.AreEqual(0, confirmedHits, "Bean hits do not publish salt powder");
    }
#endif
}

public class SaltTestReceiver : MonoBehaviour, IDamageable, ISaltReactable, IItemControlReceiver
{
    public bool Dead;
    public bool IsDead => Dead;
    public float Damage;
    public int Reactions;
    public int Controls;
    public ItemControlEffect LastControl;
    public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
    { Controls++; LastControl = effect; return true; }
    public void ReceiveDamage(float amount, GameObject source = null) => Damage += amount;
    public bool ReactToSalt(GameObject user) { Reactions++; return true; }
}

public class SaltTestLegacyReceiver : MonoBehaviour, IBeanMugwortReactable
{
    public int Reactions;
    public bool ReactToBeanMugwort(GameObject user, float slowMultiplier, float slowDuration)
    { Reactions++; return true; }
}

public class BeanTestRepelReceiver : MonoBehaviour, ISaltReactable
{
    public int Reactions;
    public bool ReactToSalt(GameObject user) { Reactions++; return true; }
}
