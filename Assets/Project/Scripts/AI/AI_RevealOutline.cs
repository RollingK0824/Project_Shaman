using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectShaman.AI
{
    // Glow outline through walls (Resources/AI/GhostRevealOutline). Every mesh renderer gets two copies that share its mesh
    // (and bones): one marks the silhouette in the stencil, one draws the swollen band, each with one material per
    // sub-mesh so a model made of several parts is outlined whole. The band is queued after every mask, so it never
    // lands inside the silhouette where two parts meet. Local to each peer; whoever owns the state (a SyncVar) decides when to show it
    public class AI_RevealOutline : MonoBehaviour
    {
        private const string MATERIAL_PATH = "AI/GhostRevealOutline";
        private const string MASK_PASS = "SRPDefaultUnlit";
        private const string BAND_PASS = "UniversalForward";

        private static Material _maskMaterial;
        private static Material _bandMaterial;
        private readonly List<Renderer> _copies = new List<Renderer>();
        private bool _isBuilt;

        public bool IsShown { get; private set; }

        public void Show(bool shown)
        {
            if (shown == IsShown)
            {
                return;
            }

            if (shown && !_isBuilt && !Build())
            {
                return;
            }

            foreach (Renderer copy in _copies)
            {
                if (copy != null)
                {
                    copy.enabled = shown;
                }
            }

            IsShown = shown;
        }

        private static bool LoadMaterials()
        {
            if (_maskMaterial != null && _bandMaterial != null)
            {
                return true;
            }

            Material source = Resources.Load<Material>(MATERIAL_PATH);
            if (source == null)
            {
                Debug.LogWarning($"[AI_RevealOutline] Resources/{MATERIAL_PATH} not found, no outline");
                return false;
            }

            _maskMaterial = new Material(source) { name = $"{source.name} (Mask)" };
            _maskMaterial.SetShaderPassEnabled(BAND_PASS, false);

            _bandMaterial = new Material(source) { name = $"{source.name} (Band)" };
            _bandMaterial.SetShaderPassEnabled(MASK_PASS, false);
            _bandMaterial.renderQueue = source.renderQueue + 1;
            return true;
        }

        private bool Build()
        {
            if (!LoadMaterials())
            {
                return false;
            }

            List<Renderer> targets = new List<Renderer>();
            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (target is MeshRenderer || target is SkinnedMeshRenderer)
                {
                    targets.Add(target);
                }
            }

            foreach (Renderer target in targets)
            {
                AddCopy(target, _maskMaterial);
                AddCopy(target, _bandMaterial);
            }

            _isBuilt = true;
            return true;
        }

        private void AddCopy(Renderer target, Material material)
        {
            Mesh mesh = target is SkinnedMeshRenderer skinned ? skinned.sharedMesh : target.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            GameObject copyObject = new GameObject($"{target.name} ({material.name})");
            copyObject.layer = target.gameObject.layer;
            copyObject.transform.SetParent(target.transform, false);

            Material[] materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            Renderer copy;
            if (target is SkinnedMeshRenderer source)
            {
                SkinnedMeshRenderer skinnedCopy = copyObject.AddComponent<SkinnedMeshRenderer>();
                skinnedCopy.sharedMesh = mesh;
                skinnedCopy.bones = source.bones;
                skinnedCopy.rootBone = source.rootBone;
                skinnedCopy.localBounds = source.localBounds;
                skinnedCopy.updateWhenOffscreen = source.updateWhenOffscreen;
                copy = skinnedCopy;
            }
            else
            {
                copyObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy = copyObject.AddComponent<MeshRenderer>();
            }

            copy.sharedMaterials = materials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = false;
            copy.enabled = false;
            _copies.Add(copy);
        }

        private void OnDestroy()
        {
            foreach (Renderer copy in _copies)
            {
                if (copy != null)
                {
                    Destroy(copy.gameObject);
                }
            }

            _copies.Clear();
        }
    }
}
