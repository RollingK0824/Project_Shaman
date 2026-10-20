using System.Collections;
using UnityEngine;
using ProjectShaman.ItemHold;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathVisual : MonoBehaviour
{
    [SerializeField] private GameObject _characterVisual;
    [SerializeField] private Animator _animator;
    [Tooltip("Optional existing Animator state, e.g. Base Layer.Death. Empty hides immediately.")]
    [SerializeField] private string _deathState;
    [SerializeField, Min(0)] private int _deathLayer;
    [SerializeField, Min(.1f)] private float _hideAfterSeconds = 3f;
    private bool _applied;
    public bool Applied => _applied;

    private void Awake()
    {
        if (_characterVisual == null) _characterVisual = transform.Find("CharacterVisual")?.gameObject;
        if (_animator == null && _characterVisual != null) _animator = _characterVisual.GetComponentInChildren<Animator>(true);
    }
    private void OnEnable() { if (GetComponent<PlayerHealth>().IsDead) ApplyDeathVisual(); }

    // Invoked by PlayerHealth after gameplay shutdown and before Died/spectator notifications.
    public void ApplyDeathVisual()
    {
        if (_applied || !GetComponent<PlayerHealth>().IsDead) return;
        _applied = true;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        if (_characterVisual == null || _characterVisual == gameObject || !_characterVisual.transform.IsChildOf(transform)) return;
        foreach (var collider in _characterVisual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in _characterVisual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
        int state = Animator.StringToHash(_deathState ?? string.Empty);
        if (_animator != null && _animator.isActiveAndEnabled && !string.IsNullOrWhiteSpace(_deathState) &&
            _deathLayer >= 0 && _deathLayer < _animator.layerCount && _animator.HasState(_deathLayer, state))
        {
            // Unequip can still be blending when death is confirmed. Stop the hold
            // driver so it cannot restore upper-body layer weight over the death pose.
            var holdDriver = _animator.GetComponent<ItemHoldDriver>();
            if (holdDriver != null) holdDriver.enabled = false;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.speed = 1f;
            for (int i = 1; i < _animator.layerCount; i++) _animator.SetLayerWeight(i, i == _deathLayer ? 1f : 0f);
            _animator.CrossFadeInFixedTime(state, .1f, _deathLayer);
            StartCoroutine(HideAfterAnimation());
        }
        else HideVisual();
    }
    private IEnumerator HideAfterAnimation() { yield return new WaitForSeconds(Mathf.Max(.1f, _hideAfterSeconds)); HideVisual(); }
    private void HideVisual() { if (_characterVisual != null && _characterVisual != gameObject) _characterVisual.SetActive(false); }
}
