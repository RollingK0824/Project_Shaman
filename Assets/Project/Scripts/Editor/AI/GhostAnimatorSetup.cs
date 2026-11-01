using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Ghost and minion models had no Animator controller, so they slid around in their bind pose. This builds AC_Ghost
// (Speed blend idle/walk/run from the humanoid villager clips, plus an Attack trigger) and puts it on every ghost and
// minion model prefab. Net_GhostEntity and Net_Minion drive Speed and fire Attack. Re-running updates what exists
public static class GhostAnimatorSetup
{
    private const string CONTROLLER_PATH = "Assets/Project/Animations/Ghost/AC_Ghost.controller";
    private const string CLIP_ROOT = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/";
    private const string IDLE_CLIP = CLIP_ROOT + "Idles/HumanM@Idle01.fbx";
    private const string WALK_CLIP = CLIP_ROOT + "Movement/Walk/HumanM@Walk01_Forward.fbx";
    private const string RUN_CLIP = CLIP_ROOT + "Movement/Run/HumanM@Run01_Forward.fbx";
    // A one-hand overhead swing reads as a strike
    private const string ATTACK_CLIP = CLIP_ROOT + "Work/Mining/HumanM@MiningOneHand01_L - Wall.fbx";

    private static readonly string[] MODEL_PREFABS =
    {
        "Assets/Project/Prefabs/AI/Ghost/Drowned_Ghost.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Dueoksini.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Faceless_Ghost.prefab",
        "Assets/Project/Prefabs/AI/Ghost/GhostGirl.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Minion/Mangyang.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Minion/Starvation_Ghost.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Minion/Yagwanggwi.prefab",
    };

    [MenuItem("ProjectShaman/AI/Setup Ghost Animators")]
    private static void Setup()
    {
        AnimatorController controller = CreateController();
        foreach (string path in MODEL_PREFABS)
        {
            Assign(path, controller);
        }

        AssetDatabase.SaveAssets();
    }

    private static AnimatorController CreateController()
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CONTROLLER_PATH));
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(LoadClip(IDLE_CLIP), 0f);
        tree.AddChild(LoadClip(WALK_CLIP), 1.5f);
        tree.AddChild(LoadClip(RUN_CLIP), 4f);
        root.defaultState = locomotion;

        AnimatorState attack = root.AddState("Attack");
        attack.motion = string.IsNullOrEmpty(ATTACK_CLIP) ? null : LoadClip(ATTACK_CLIP);

        AnimatorStateTransition toAttack = root.AddAnyStateTransition(attack);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.duration = 0.1f;
        toAttack.canTransitionToSelf = false;

        AnimatorStateTransition back = attack.AddTransition(locomotion);
        back.hasExitTime = true;
        back.exitTime = 0.9f;
        back.duration = 0.15f;

        Debug.Log($"[GhostAnimator] {CONTROLLER_PATH}: idle/walk/run by Speed, Attack {(attack.motion != null ? attack.motion.name : "(no clip yet)")}");
        return controller;
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        Debug.LogWarning($"[GhostAnimator] no clip in {path}");
        return null;
    }

    private static void Assign(string path, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            if (animator.avatar == null)
            {
                animator.avatar = FindAvatar(root);
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[GhostAnimator] {path}: AC_Ghost on {animator.name}, avatar {(animator.avatar != null ? animator.avatar.name : "none")}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The humanoid avatar the model's FBX imported
    private static Avatar FindAvatar(GameObject root)
    {
        foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string meshPath = AssetDatabase.GetAssetPath(skin.sharedMesh);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(meshPath))
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }
        }

        return null;
    }
}
