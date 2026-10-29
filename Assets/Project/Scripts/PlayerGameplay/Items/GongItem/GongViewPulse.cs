using UnityEngine;

// Only the local first-person presentation follows the camera. No gameplay area moves with it.
[DefaultExecutionOrder(350)]
public sealed class GongViewPulse : MonoBehaviour
{
    public static readonly Vector3 CameraOffset = new(0f, -.1f, 1.5f);
    private GameObject _owner;
    private Camera _camera;
    private PlayerViewModeController _view;

    public void Bind(GameObject owner, Camera camera, PlayerViewModeController view)
    {
        _owner = owner; _camera = camera; _view = view;
        foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
        Follow();
    }

    private void LateUpdate()
    {
        if (_owner == null || _camera == null || !_camera.isActiveAndEnabled ||
            !PlayerActionGuard.CanAct(_owner) ||
            (_view != null && _view.CurrentViewMode != PlayerViewModeController.ViewMode.FirstPerson))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Follow();
    }

    private void Follow()
    {
        if (_camera == null) return;
        // Preserve prefab scale; a scaled player/camera parent must not enlarge the pulse.
        transform.SetPositionAndRotation(_camera.transform.position + _camera.transform.rotation * CameraOffset,
            _camera.transform.rotation);
    }
}
