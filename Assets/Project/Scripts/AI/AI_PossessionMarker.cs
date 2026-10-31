using System;
using UnityEngine;

namespace ProjectShaman.AI
{
    // Test aid: marks a possessed villager with a yellow glow outline so a tester can see who the hosts are.
    // Off by default on every peer (the test is whether players can deduce hosts without it); the F1 test panel turns it on
    // for that peer. An outline (AI_RevealOutline copies of the renderers), not a material swap, so it never fights the
    // symptom tint on the body's own materials and follows a body that is replaced for the villager's gender.
    public class AI_PossessionMarker : MonoBehaviour
    {
        private static readonly Color MARK_COLOR = new Color(1f, 0.85f, 0.1f, 1f);

        private AI_RevealOutline _outline;
        private AI_VillagerModel _model;
        private bool _marked;

        // Whether this peer shows the marks (the F1 panel toggles it)
        public static bool Visible { get; private set; }
        public static event Action<bool> VisibilityChanged;

        public bool IsMarked => _marked;

        public static void SetVisible(bool visible)
        {
            if (visible == Visible)
            {
                return;
            }

            Visible = visible;
            VisibilityChanged?.Invoke(visible);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Visible = false;
            VisibilityChanged = null;
        }

        // marked = the server says this villager is possessed (synced to every peer); shown only while Visible
        public void SetMarked(bool marked)
        {
            _marked = marked;
            Refresh();
        }

        private void Awake()
        {
            TryGetComponent(out _model);
            if (_model != null)
            {
                _model.OnModelChanged += HandleModelChanged;
            }
        }

        private void OnEnable()
        {
            VisibilityChanged += HandleVisibility;
            Refresh();
        }

        private void OnDisable()
        {
            VisibilityChanged -= HandleVisibility;
        }

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.OnModelChanged -= HandleModelChanged;
            }
        }

        private void HandleVisibility(bool visible)
        {
            Refresh();
        }

        // The body was swapped: the outline copies went with the old one, so they are built again on the new one
        private void HandleModelChanged(Animator animator)
        {
            if (_outline != null)
            {
                _outline.Invalidate();
            }

            Refresh();
        }

        private void Refresh()
        {
            bool show = _marked && Visible;
            if (_outline == null)
            {
                if (!show)
                {
                    return;
                }

                _outline = gameObject.AddComponent<AI_RevealOutline>();
                _outline.SetColor(MARK_COLOR);
            }

            _outline.Show(show);
        }
    }
}
