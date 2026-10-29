using UnityEngine;

public class GongItem : ItemBase
{
    [SerializeField] private ParticleSystem _pulse;
    [SerializeField] private AudioSource _audio;
    public override void OnUseStarted()
    {
        if (!TryBuildUseRequest(out var context)) return;
        if (!ItemVfxPresentation.Pulse(Owner, true) && _pulse != null) _pulse.Play();
        // Audio is owned by PlayerItemFeedback; this legacy source stays serialized for compatibility.
        PlayerEvents.RaiseItemUseRequested(context);
    }
}
