using UnityEngine;

// Optional explicit surface override; an unmarked surface uses the dirt footsteps.
// Add to a wood floor collider (or its parent) when the map owner opts in.
[DisallowMultipleComponent]
public sealed class PlayerFootstepSurface : MonoBehaviour
{
    public bool Wood = true;
}
