using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI
{
    // The body changes of a possession's appearance symptoms, on every peer from AI_Core.AppearanceMask:
    //  NoShadow  - the body casts no shadow
    //  PaleSkin  - skin material tinted toward a corpse-grey
    //  BlackEyes - eye material tinted black
    // Skin and eyes are found by material name (Mat_Body / M_..._Skin, Mat_Eye / M_..._Eyes), so both gender models work.
    // A tinted renderer gets its own material instance (the shared material would change every villager) and the original
    // colour comes back when the episode ends. Added to the villager at runtime by AI_VisualController.
    [RequireComponent(typeof(AI_Core))]
    public class AI_SymptomVisual : MonoBehaviour
    {
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR = Shader.PropertyToID("_Color");

        [Tooltip("Skin colour is multiplied by this at full pale")]
        [SerializeField] private Color _paleTint = new Color(0.62f, 0.7f, 0.74f, 1f);
        [Tooltip("Eye colour is multiplied by this at full black")]
        [SerializeField] private Color _blackEyeTint = new Color(0.02f, 0.02f, 0.02f, 1f);
        [Tooltip("Seconds the skin and eyes take to change")]
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 1.5f;

        private sealed class Tinted
        {
            public Renderer Renderer;
            public int Slot;
            public bool IsEye;
            public int ColorId;
            public Color Original;
            public Material Instance;
        }

        private AI_Core _core;
        private AI_VillagerModel _model;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<ShadowCastingMode> _shadowModes = new List<ShadowCastingMode>();
        private readonly List<Tinted> _tinted = new List<Tinted>();
        private float _pale;
        private float _black;
        private int _mask;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _model = GetComponent<AI_VillagerModel>();
            _core.OnAppearanceChanged += SetMask;
            if (_model != null)
            {
                _model.OnModelChanged += HandleModelChanged;
            }

            // Nothing to animate until a symptom shows
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_core != null)
            {
                _core.OnAppearanceChanged -= SetMask;
            }

            if (_model != null)
            {
                _model.OnModelChanged -= HandleModelChanged;
            }

            ReleaseInstances();
        }

        private void Start()
        {
            // A client joining mid-episode had its mask applied before this component existed
            if (_core.AppearanceMask != 0)
            {
                SetMask(_core.AppearanceMask);
            }
        }

        private void SetMask(int mask)
        {
            _mask = mask;
            Collect();
            ApplyShadow();
            enabled = true;
        }

        // The body was swapped for the villager's gender: collect the new renderers and show the current state on them
        private void HandleModelChanged(Animator newAnimator)
        {
            _pale = 0f;
            _black = 0f;
            Collect(true);
            ApplyShadow();
            enabled = _mask != 0;
        }

        private void Update()
        {
            float step = Time.deltaTime / _fadeSeconds;
            _pale = Mathf.MoveTowards(_pale, Has(SymptomType.PaleSkin) ? 1f : 0f, step);
            _black = Mathf.MoveTowards(_black, Has(SymptomType.BlackEyes) ? 1f : 0f, step);

            foreach (Tinted tinted in _tinted)
            {
                if (tinted.Instance == null)
                {
                    continue;
                }

                Color target = tinted.IsEye ? _blackEyeTint : _paleTint;
                float weight = tinted.IsEye ? _black : _pale;
                tinted.Instance.SetColor(tinted.ColorId, Color.Lerp(tinted.Original, tinted.Original * target, weight));
            }

            if (_mask == 0 && _pale <= 0f && _black <= 0f)
            {
                enabled = false;
            }
        }

        private bool Has(SymptomType symptom)
        {
            return (_mask & (1 << (int)symptom)) != 0;
        }

        private void ApplyShadow()
        {
            bool off = Has(SymptomType.NoShadow);
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].shadowCastingMode = off ? ShadowCastingMode.Off : _shadowModes[i];
                }
            }
        }

        // Renderers of the body in use. Collected once per body, the first symptom after that finds them ready
        private void Collect(bool force = false)
        {
            if (_renderers.Count > 0 && !force)
            {
                return;
            }

            ReleaseInstances();
            _renderers.Clear();
            _shadowModes.Clear();

            Transform root = _model != null && _model.CurrentModel != null ? _model.CurrentModel : transform;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
                {
                    continue;
                }

                _renderers.Add(renderer);
                _shadowModes.Add(renderer.shadowCastingMode);
                CollectTinted(renderer);
            }
        }

        private void CollectTinted(Renderer renderer)
        {
            Material[] shared = renderer.sharedMaterials;
            Material[] instances = null;

            for (int slot = 0; slot < shared.Length; slot++)
            {
                Material material = shared[slot];
                if (material == null)
                {
                    continue;
                }

                bool isEye = IsEyeMaterial(material.name);
                bool isSkin = !isEye && IsSkinMaterial(material.name);
                int colorId = material.HasProperty(BASE_COLOR) ? BASE_COLOR : material.HasProperty(COLOR) ? COLOR : 0;
                if ((!isEye && !isSkin) || colorId == 0)
                {
                    continue;
                }

                // One instance of this renderer's materials, so the shared asset keeps its colour
                if (instances == null)
                {
                    instances = renderer.materials;
                }

                _tinted.Add(new Tinted
                {
                    Renderer = renderer,
                    Slot = slot,
                    IsEye = isEye,
                    ColorId = colorId,
                    Original = material.GetColor(colorId),
                    Instance = instances[slot]
                });
            }
        }

        private static bool IsEyeMaterial(string materialName)
        {
            return materialName.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0
                && materialName.IndexOf("Eyebrow", System.StringComparison.OrdinalIgnoreCase) < 0
                && materialName.IndexOf("Eyelash", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsSkinMaterial(string materialName)
        {
            return materialName.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0
                || materialName.IndexOf("Skin", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ReleaseInstances()
        {
            foreach (Tinted tinted in _tinted)
            {
                if (tinted.Instance != null)
                {
                    Destroy(tinted.Instance);
                }
            }

            _tinted.Clear();
        }
    }
}
