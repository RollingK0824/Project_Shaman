using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI
{
    public class AI_PossessionMarker : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material[]> _originals = new List<Material[]>();

        public bool IsMarked { get; private set; }

        public void SetMarked(bool marked, Material material)
        {
            if (marked == IsMarked || (marked && material == null))
            {
                return;
            }

            if (marked)
            {
                Apply(material);
            }
            else
            {
                Restore();
            }

            IsMarked = marked;
        }

        private void Apply(Material material)
        {
            _renderers.Clear();
            _originals.Clear();

            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] original = target.sharedMaterials;
                Material[] replaced = new Material[original.Length];
                for (int i = 0; i < replaced.Length; i++)
                {
                    replaced[i] = material;
                }

                _renderers.Add(target);
                _originals.Add(original);
                target.sharedMaterials = replaced;
            }
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
    }
}
