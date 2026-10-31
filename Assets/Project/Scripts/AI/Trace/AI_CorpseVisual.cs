using System;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Trace
{
    // The body a corpse trace shows, picked by GhostTrace.VisualKey: the victim's gender for a villager (Male / Female),
    // the animal kind for livestock (Cattle / Chicken / Pig). The server sets the key when the night action leaves the trace;
    // Net_GhostTrace carries it to clients. Until a key arrives (or none matches) the placeholder stays.
    // A model with an Animator plays DieState then PoseState (the lying-dead frame) when its PoseController has them;
    // otherwise LocalEuler lays the body down on a frozen frame.
    [RequireComponent(typeof(GhostTrace))]
    public class AI_CorpseVisual : MonoBehaviour
    {
        [Serializable]
        public class Variant
        {
            public string Key;
            public GameObject Model;
            [Tooltip("Animator controller with the dead pose state, optional")]
            public RuntimeAnimatorController PoseController;
            [Tooltip("Played once when the corpse appears, then the controller moves on to PoseState by itself (optional)")]
            public string DieState = "Die";
            public string PoseState = "Dead";
            public Vector3 LocalPosition;
            [Tooltip("Lays the body down while there is no dead pose clip (set to 0 once PoseState lays it down)")]
            public Vector3 LocalEuler;
            public Vector3 LocalScale = Vector3.one;
        }

        [Tooltip("Shown until a variant is chosen, then only its renderers are hidden; its collider stays as the clear target")]
        [SerializeField] private GameObject _placeholder;
        [SerializeField] private Variant[] _variants = Array.Empty<Variant>();

        private GhostTrace _trace;
        private GameObject _model;
        private string _shownKey;

        private void Awake()
        {
            _trace = GetComponent<GhostTrace>();
            _trace.OnVisualKeyChanged += Show;
        }

        private void Start()
        {
            Show(_trace.VisualKey);
        }

        private void OnDestroy()
        {
            if (_trace != null)
            {
                _trace.OnVisualKeyChanged -= Show;
            }
        }

        private void Show(string key)
        {
            if (string.IsNullOrEmpty(key) || key == _shownKey)
            {
                return;
            }

            Variant variant = Array.Find(_variants, v => v != null && v.Model != null && v.Key == key);
            if (variant == null)
            {
                AILog.Warn(AILog.TRACE, $"{name}: no corpse model for '{key}', placeholder kept");
                return;
            }

            if (_model != null)
            {
                Destroy(_model);
            }

            _shownKey = key;
            _model = Instantiate(variant.Model, transform, false);
            _model.transform.localPosition = variant.LocalPosition;
            _model.transform.localRotation = Quaternion.Euler(variant.LocalEuler);
            _model.transform.localScale = variant.LocalScale;

            // A corpse never moves: no scripts, physics or network parts of the source model
            foreach (Collider part in _model.GetComponentsInChildren<Collider>(true))
            {
                part.enabled = false;
            }

            Animator animator = _model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (variant.PoseController != null)
                {
                    animator.runtimeAnimatorController = variant.PoseController;
                }

                int die = Animator.StringToHash(variant.DieState ?? string.Empty);
                int pose = Animator.StringToHash(variant.PoseState ?? string.Empty);
                bool hasController = animator.runtimeAnimatorController != null;

                if (hasController && animator.HasState(0, die))
                {
                    // Falls, then the controller's Die → Dead transition holds the body down
                    animator.Play(die, 0, 0f);
                }
                else
                {
                    if (hasController && animator.HasState(0, pose))
                    {
                        animator.Play(pose, 0, 0f);
                    }

                    // Held on that frame (the dead pose, or the first idle frame laid down by LocalEuler): a body does not breathe
                    animator.Update(0f);
                    animator.speed = 0f;
                }
            }

            // Only the look goes: the placeholder's collider is what the player's interaction ray hits to clear the body
            if (_placeholder != null)
            {
                foreach (Renderer part in _placeholder.GetComponentsInChildren<Renderer>(true))
                {
                    part.enabled = false;
                }
            }
        }
    }
}
