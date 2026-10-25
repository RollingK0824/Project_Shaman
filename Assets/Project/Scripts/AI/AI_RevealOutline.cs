using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI
{
    // Glow outline through walls (Resources/AI/GhostRevealOutline): appended as one more material on every mesh renderer
    // while shown, removed again after. Local to each peer; whoever owns the state (a SyncVar) decides when to show it.
    // A renderer with several sub-meshes gets the outline on its last one only
    public class AI_RevealOutline : MonoBehaviour
    {
        private const string MATERIAL_PATH = "AI/GhostRevealOutline";

        private static Material _material;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material[]> _originals = new List<Material[]>();

        public bool IsShown { get; private set; }

        public void Show(bool shown)
        {
            if (shown == IsShown)
            {
                return;
            }

            if (shown && !Apply())
            {
                return;
            }

            if (!shown)
            {
                Restore();
            }

            IsShown = shown;
        }

        private bool Apply()
        {
            if (_material == null)
            {
                _material = Resources.Load<Material>(MATERIAL_PATH);
                if (_material == null)
                {
                    Debug.LogWarning($"[AI_RevealOutline] Resources/{MATERIAL_PATH} not found, no outline");
                    return false;
                }
            }

            _renderers.Clear();
            _originals.Clear();

            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] original = target.sharedMaterials;
                Material[] withOutline = new Material[original.Length + 1];
                original.CopyTo(withOutline, 0);
                withOutline[original.Length] = _material;

                _renderers.Add(target);
                _originals.Add(original);
                target.sharedMaterials = withOutline;
            }

            return true;
        }

        private void Restore()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].sharedMaterials = _originals[i];
                }
            }

            _renderers.Clear();
            _originals.Clear();
        }

        private void OnDestroy()
        {
            Restore();
        }
    }
}
