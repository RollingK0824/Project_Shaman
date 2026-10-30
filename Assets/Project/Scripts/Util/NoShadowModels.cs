#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The terrain paints these Coastal Dune plants as raw models, and a model has no Cast Shadows setting of its own.
// Turns shadows off on them at import, so every painted copy stops casting. Reimport the models after changing this list
public class NoShadowModels : AssetPostprocessor
{
    private static readonly string[] PATHS =
    {
        "Assets/ThirdParty/Coastal_Dune_Pack/Meshes/SM_Anthephoroides.fbx",
        "Assets/ThirdParty/Coastal_Dune_Pack/Meshes/SM_Artemisia.fbx",
        "Assets/ThirdParty/Coastal_Dune_Pack/Meshes/SM_Eulalia.fbx",
        "Assets/ThirdParty/Coastal_Dune_Pack/Meshes/SM_Lugose.fbx",
        "Assets/ThirdParty/Coastal_Dune_Pack/Meshes/SM_Vitex.fbx",
    };

    public override uint GetVersion()
    {
        return 1;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (System.Array.IndexOf(PATHS, assetPath) < 0)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
#endif
