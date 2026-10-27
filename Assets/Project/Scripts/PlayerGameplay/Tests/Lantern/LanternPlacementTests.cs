#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class LanternPlacementTests
{
    readonly List<GameObject> _objects = new();
    static readonly Vector3 Origin = new Vector3(1500, 100, 1500);
    GameObject Make(string name) { var go = new GameObject(name); _objects.Add(go); return go; }
    ItemData _lantern;
    PlayerInventory _inventory;
    PlayerLanternPlacement _placement;
    PlayerItemController _items;

    [SetUp] public void Setup()
    {
        _lantern = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/LanternItemData.asset");
        var player = Make("Lantern placement test player"); player.transform.position = Origin;
        _items = player.AddComponent<PlayerItemController>();
        _inventory = player.GetComponent<PlayerInventory>();
        _placement = player.AddComponent<PlayerLanternPlacement>();
        Assert.IsTrue(_inventory.TryAddItem(_lantern));
        typeof(PlayerItemController).GetField("<EquippedItemData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_items, _lantern);
    }
    [TearDown] public void Cleanup()
    {
        for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i]) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
    }
    BoxCollider Pillar(Vector3 center, Quaternion rotation)
    {
        var go = Make("Support pillar"); go.transform.SetPositionAndRotation(center, rotation);
        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(.5f, 3, .5f); return box;
    }

    [UnityTest] public IEnumerator RealLanternPrefabStaysOnViewedSideOfFourPillarFacesAndObliqueFace()
    {
        var center = Origin + Vector3.forward * 2 + Vector3.up;
        var pillar = Pillar(center, Quaternion.identity);
        var normals = new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left,
            new Vector3(1, 0, -1).normalized, new Vector3(0, .15f, -1).normalized };
        long request = 0;
        foreach (var normal in normals)
        {
            pillar.transform.rotation = Quaternion.LookRotation(-normal);
            var hitPoint = center + normal * .25f + Vector3.up * .35f;
            var eye = hitPoint + normal * 1.5f;
            _placement.transform.position = eye - Vector3.up;
            var aim = new Ray(eye, -normal);
            Physics.SyncTransforms();
            Assert.IsTrue(_placement.TryGetWall(aim, out var hit));
            Assert.IsTrue(_placement.TryPlaceAuthoritative(++request, aim, () => true, null, null, null, out var world), "Placement failed at " + aim);
            _objects.Add(world.gameObject);
            Assert.IsFalse(_inventory.Contains(_lantern));
            Assert.IsTrue(world.GetComponent<LanternWorldPresentation>().IsMounted);
            var renderer = world.GetComponentInChildren<MeshRenderer>();
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            float min = float.PositiveInfinity;
            // Check every visible mesh vertex against the supporting plane, independently of placement bounds.
            foreach (var point in mesh.vertices)
                min = Mathf.Min(min, Vector3.Dot(renderer.transform.TransformPoint(point) - hit.point, normal));
            Assert.GreaterOrEqual(min, .003f, "Rendered lantern penetrated support plane");
            Assert.Greater(Vector3.Dot(world.transform.forward, Vector3.ProjectOnPlane(normal, Vector3.up).normalized), .999f);
            Assert.Greater(Vector3.Dot(world.transform.up, Vector3.up), .999f, "Lantern must hang upright on sloped surfaces");
            Assert.Less(Mathf.Abs(renderer.bounds.max.y - hit.point.y), .005f, "Handle top must remain at aimed height");
            Assert.IsFalse(Physics.ComputePenetration(world.GetComponent<BoxCollider>(), world.transform.position, world.transform.rotation,
                pillar, pillar.transform.position, pillar.transform.rotation, out _, out _), "Pickup collider penetrates pillar");
            Assert.IsFalse(_placement.TryPlaceAuthoritative(request, aim, () => true, null, null, null, out _), "Replay cannot duplicate lantern");
            Object.Destroy(world.gameObject); yield return null;
            Assert.IsTrue(_inventory.TryAddItem(_lantern));
            typeof(PlayerItemController).GetField("<EquippedItemData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_items, _lantern);
        }
    }

    [UnityTest] public IEnumerator ObstructionAndDeniedAuthorityKeepInventoryUnchanged()
    {
        Pillar(Origin + Vector3.forward * 2 + Vector3.up, Quaternion.identity);
        var aim = new Ray(Origin + Vector3.up * 1.35f, Vector3.forward);
        Physics.SyncTransforms();
        Assert.IsFalse(_placement.TryPlaceAuthoritative(1, aim, () => false, null, null, null, out _));
        Assert.IsTrue(_inventory.Contains(_lantern));
        var shelf = Make("Shelf below aim"); shelf.transform.position = Origin + new Vector3(0, 1.08f, 1.5f);
        shelf.AddComponent<BoxCollider>().size = new Vector3(1, .15f, .4f);
        Physics.SyncTransforms();
        Assert.IsTrue(_placement.TryGetWall(aim, out _));
        Assert.IsFalse(_placement.TryPlaceAuthoritative(2, aim, () => true, null, null, null, out _));
        Assert.IsTrue(_inventory.Contains(_lantern));
        yield return null;
    }

    [UnityTest] public IEnumerator VisibleSupportMustBeHitAndItsGeometryMustRemainClear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Naganeupseong/Prefabs/Structure/Pillar/Pillar01a.prefab");
        var pillar = Object.Instantiate(prefab, Origin + Vector3.forward * 2, Quaternion.identity);
        _objects.Add(pillar);
        var aim = new Ray(Origin + new Vector3(.14f, 1.7f, 0), Vector3.forward);
        Physics.SyncTransforms();
        Assert.IsFalse(_placement.TryGetWall(aim, out _), "Empty space inside the coarse hull cannot support a lantern");
        Assert.IsFalse(_placement.TryPlaceAuthoritative(1, aim, () => true, null, null, null, out _));
        Assert.IsTrue(_inventory.Contains(_lantern));

        // A visible ledge has no separate collider: it belongs to the same coarse support.
        var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube); _objects.Add(ledge);
        Object.DestroyImmediate(ledge.GetComponent<BoxCollider>());
        ledge.transform.SetParent(pillar.transform, false);
        ledge.transform.localPosition = new Vector3(.1f, 1.45f, -.1f);
        ledge.transform.localScale = new Vector3(.05f, .08f, .2f);
        aim = new Ray(Origin + Vector3.up * 1.7f, Vector3.forward);
        Physics.SyncTransforms();
        Assert.IsTrue(_placement.TryGetWall(aim, out _));
        Assert.IsFalse(_placement.TryPlaceAuthoritative(2, aim, () => true, null, null, null, out _), "Lantern must not clip the visible support ledge");
        Assert.IsTrue(_inventory.Contains(_lantern));
        yield return null;
    }

    [UnityTest] public IEnumerator ActualPillarPrefabUsesNearestFaceDespiteInwardCollisionTriangles()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Naganeupseong/Prefabs/Structure/Pillar/Pillar01a.prefab");
        Assert.NotNull(prefab);
        var pillar = Object.Instantiate(prefab, Origin + Vector3.forward * 2, Quaternion.identity);
        _objects.Add(pillar);
        Physics.SyncTransforms();
        var support = pillar.GetComponent<Collider>();
        var center = support.bounds.center; center.y = Origin.y + 1.7f;
        bool original = Physics.queriesHitBackfaces;
        try
        {
            for (int angle = 0; angle < 8; angle++)
            {
                Physics.queriesHitBackfaces = angle % 2 == 0;
                bool expectedSetting = Physics.queriesHitBackfaces;
                var outward = Quaternion.Euler(0, angle * 45, 0) * Vector3.back;
                var eye = center + outward * 1.6f;
                _placement.transform.position = eye - Vector3.up * 1.8f;
                var aim = new Ray(eye, -outward);
                Physics.SyncTransforms();
                Assert.IsTrue(_placement.TryGetWall(aim, out var hit));
                Assert.AreSame(support, hit.collider);
                Assert.Greater(Vector3.Dot(hit.point - center, outward), .06f, "Selected the far face of the pillar");
                Assert.Greater(Vector3.Dot(hit.normal, outward), .5f);
                Assert.AreEqual(expectedSetting, Physics.queriesHitBackfaces, "Query leaked a global physics setting");
                Assert.IsTrue(_placement.TryPlaceAuthoritative(angle + 1, aim, () => true, null, null, null, out var world), "Placement failed at " + aim);
                _objects.Add(world.gameObject);
                Assert.IsFalse(_inventory.Contains(_lantern));
                float minimum = float.PositiveInfinity;
                var renderer = world.GetComponentInChildren<MeshRenderer>();
                foreach (var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                    minimum = Mathf.Min(minimum, Vector3.Dot(renderer.transform.TransformPoint(vertex) - hit.point, hit.normal));
                Assert.GreaterOrEqual(minimum, .003f);
                Assert.LessOrEqual(minimum, .007f, "Lantern floats away from the visible support plane");
                // The coarse hull overlaps by design; clearance is measured against the visible surface.
                Object.Destroy(world.gameObject); yield return null;
                Assert.IsTrue(_inventory.TryAddItem(_lantern));
                typeof(PlayerItemController).GetField("<EquippedItemData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_items, _lantern);
            }
        }
        finally { Physics.queriesHitBackfaces = original; }
    }
}
#endif
