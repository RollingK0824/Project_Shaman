using System.Collections.Generic;
using UnityEngine;

// Presentation only: authoritative root, colliders and pickup ownership stay at the validated destination.
// Network adapter may call Play once on the spawned WorldItem after receiving a confirmed drop event.
[DisallowMultipleComponent]
public sealed class ItemDropPresentation : MonoBehaviour
{
    private readonly List<Renderer> _sources = new List<Renderer>();
    private readonly List<bool> _hidden = new List<bool>();
    private readonly List<Mesh> _bakedMeshes = new List<Mesh>();
    private GameObject _visual;
    private Vector3 _start, _end;
    private float _elapsed, _duration;
    public bool IsPlaying => _visual != null;

    public void Play(Vector3 releasePoint)
    {
        if (IsPlaying || !isActiveAndEnabled || !float.IsFinite(releasePoint.sqrMagnitude)) return;
        Bounds bounds = default;
        bool found = false;
        foreach (var source in GetComponentsInChildren<Renderer>())
        {
            if (!source.enabled || source.forceRenderingOff ||
                (!(source is MeshRenderer) && !(source is SkinnedMeshRenderer))) continue;
            if (!found) { bounds = source.bounds; found = true; }
            else bounds.Encapsulate(source.bounds);
            _sources.Add(source);
            _hidden.Add(source.forceRenderingOff);
        }
        if (!found) return;
        _start = releasePoint;
        _end = bounds.center;
        _duration = Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Max(.2f, _start.y - _end.y) / 9.81f), .3f, .65f);
        _elapsed = 0f;
        _visual = new GameObject("Dropped item visual");
        _visual.transform.position = _end;
        foreach (var source in _sources)
        {
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skin)
            {
                mesh = new Mesh();
                skin.BakeMesh(mesh);
                _bakedMeshes.Add(mesh);
            }
            else if (source.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.layer = source.gameObject.layer;
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            part.transform.localScale = source.transform.lossyScale;
            part.transform.SetParent(_visual.transform, true);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
            source.forceRenderingOff = true;
        }
        _visual.transform.position = _start;
    }

    private void Update()
    {
        if (!IsPlaying) return;
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        // Horizontal travel is steady; vertical travel accelerates toward the floor.
        var position = Vector3.Lerp(_start, _end, t);
        position.y = Mathf.Lerp(_start.y, _end.y, t * t);
        _visual.transform.position = position;
        _visual.transform.rotation = Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 18f, 0f, Mathf.Sin(t * Mathf.PI) * -10f);
        if (t >= 1f) Clear();
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
    private void Clear()
    {
        for (int i = 0; i < _sources.Count; i++)
            if (_sources[i] != null) _sources[i].forceRenderingOff = _hidden[i];
        _sources.Clear();
        _hidden.Clear();
        if (_visual != null) { _visual.SetActive(false); Destroy(_visual); }
        _visual = null;
        foreach (var mesh in _bakedMeshes) if (mesh != null) Destroy(mesh);
        _bakedMeshes.Clear();
    }
}
