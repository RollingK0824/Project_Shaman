using System.Collections.Generic;
using UnityEngine;

// Read-only geometry queries for static supports whose collision hull differs
// from their visible mesh. No environment collider or importer is modified.
internal static class LanternMountSurface
{
    private sealed class Geometry
    {
        public Vector3[] Vertices;
        public int[] Triangles;
    }
    private static readonly Dictionary<Mesh, Geometry> Cache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => Cache.Clear();

    private static Geometry Read(Mesh mesh)
    {
        if (!Cache.TryGetValue(mesh, out var data))
        {
            data = new Geometry { Vertices = mesh.vertices, Triangles = mesh.triangles };
            Cache.Add(mesh, data);
        }
        return data;
    }

    public static bool TryRefine(Collider support, Ray ray, float distance,
        out MeshFilter[] surfaces, out Vector3 point, out Vector3 normal)
    {
        surfaces = null; point = normal = default;
        if (!(support is MeshCollider collider)) return false;
        var filters = support.GetComponentsInChildren<MeshFilter>();
        var visible = new List<MeshFilter>();
        foreach (var filter in filters)
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.enabled && !renderer.forceRenderingOff && filter.sharedMesh != null)
            {
                // An unreadable or matching surface retains the original physics path.
                if (!filter.sharedMesh.isReadable || filter.sharedMesh == collider.sharedMesh) return false;
                visible.Add(filter);
            }
        }
        if (visible.Count == 0) return false;
        var candidates = visible.ToArray();
        surfaces = candidates;
        return Raycast(candidates, ray, distance, out point, out normal);
    }

    public static bool Raycast(MeshFilter[] surfaces, Ray ray, float distance, out Vector3 point, out Vector3 normal)
    {
        point = normal = default;
        float closest = distance;
        bool found = false;
        foreach (var filter in surfaces)
        {
            var data = Read(filter.sharedMesh);
            var inverse = filter.transform.worldToLocalMatrix;
            // Do not normalize: the parameter remains the world-space ray distance.
            var origin = inverse.MultiplyPoint3x4(ray.origin);
            var direction = inverse.MultiplyVector(ray.direction);
            for (int i = 0; i < data.Triangles.Length; i += 3)
            {
                var a = data.Vertices[data.Triangles[i]];
                var e1 = data.Vertices[data.Triangles[i + 1]] - a;
                var e2 = data.Vertices[data.Triangles[i + 2]] - a;
                var cross = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, cross);
                if (Mathf.Abs(det) < 1e-8f) continue;
                var delta = origin - a;
                float u = Vector3.Dot(delta, cross) / det;
                if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(delta, e1);
                float v = Vector3.Dot(direction, q) / det;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(e2, q) / det;
                if (t < 0f || t > closest) continue;
                closest = t; found = true;
                normal = inverse.transpose.MultiplyVector(Vector3.Cross(e1, e2)).normalized;
                if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal;
            }
        }
        if (found) point = ray.GetPoint(closest);
        return found;
    }

    public static bool Overlaps(MeshFilter[] surfaces, Vector3 center, Vector3 extents, Quaternion rotation)
    {
        var toBox = Matrix4x4.TRS(center, rotation, Vector3.one).inverse;
        foreach (var filter in surfaces)
        {
            var data = Read(filter.sharedMesh);
            var matrix = toBox * filter.transform.localToWorldMatrix;
            for (int i = 0; i < data.Triangles.Length; i += 3)
            {
                var a = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[i]]);
                var b = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[i + 1]]);
                var c = matrix.MultiplyPoint3x4(data.Vertices[data.Triangles[i + 2]]);
                if (Separated(Vector3.right, a, b, c, extents) || Separated(Vector3.up, a, b, c, extents) ||
                    Separated(Vector3.forward, a, b, c, extents)) continue;
                var ab = b - a; var bc = c - b; var ca = a - c;
                if (Separated(Vector3.Cross(ab, bc), a, b, c, extents) ||
                    EdgeSeparated(ab, a, b, c, extents) || EdgeSeparated(bc, a, b, c, extents) ||
                    EdgeSeparated(ca, a, b, c, extents)) continue;
                return true;
            }
        }
        return false;
    }

    private static bool EdgeSeparated(Vector3 edge, Vector3 a, Vector3 b, Vector3 c, Vector3 extents) =>
        Separated(Vector3.Cross(edge, Vector3.right), a, b, c, extents) ||
        Separated(Vector3.Cross(edge, Vector3.up), a, b, c, extents) ||
        Separated(Vector3.Cross(edge, Vector3.forward), a, b, c, extents);

    private static bool Separated(Vector3 axis, Vector3 a, Vector3 b, Vector3 c, Vector3 extents)
    {
        float radius = Vector3.Dot(extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
        float pa = Vector3.Dot(a, axis), pb = Vector3.Dot(b, axis), pc = Vector3.Dot(c, axis);
        return Mathf.Min(pa, Mathf.Min(pb, pc)) > radius || Mathf.Max(pa, Mathf.Max(pb, pc)) < -radius;
    }
}
