using System;
using UnityEngine;

// UI-only avatar lookup. Photos are captured from the existing player models.
[CreateAssetMenu(menuName = "Project Shaman/UI/Notebook Player Portraits")]
public sealed class NotebookPlayerPortraits : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public Avatar Avatar;
        public Texture2D Portrait;
    }
    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    public Texture2D Find(GameObject player)
    {
        if (player == null) return null;
        var visual = player.transform.Find("CharacterVisual");
        if (visual == null) return null;
        var animators = visual.GetComponentsInChildren<Animator>(true);
        // Prefer the selected active model; still show the owner's portrait after death hides the visual.
        foreach (var animator in animators)
            if (animator.gameObject.activeInHierarchy && Find(animator.avatar) is Texture2D portrait) return portrait;
        if (animators.Length == 1) return Find(animators[0].avatar);
        return null;
    }

    private Texture2D Find(Avatar avatar)
    {
        if (avatar == null) return null;
        foreach (var entry in _entries)
            if (entry.Avatar == avatar) return entry.Portrait;
        return null;
    }
}
