using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Bakes the villager routine clips on the reference model (HumanM_Model): each frame starts from the Kevin Iglesias idle,
// bends the body by hand-written keys, places hands and feet with two-bone IK and is read back as Humanoid muscles
public static class VillagerClipBaker
{
    private const string MODEL_PATH = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
    private const string IDLE_PATH = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string GRIP_PATH = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Work/Hammering/HumanM@HammeringGround01_R - Loop.fbx";
    private static readonly string[] VILLAGER_MODELS =
    {
        "Assets/Project/Visuals/Models/NPC/Male/Model/Male_Villager.fbx",
        "Assets/Project/Visuals/Models/NPC/Female/Model/NPC_Female.fbx",
    };

    private const string CLIP_FOLDER = "Assets/Project/Animations/Npc/Clip/";
    private const string CONTROLLER_PATH = "Assets/Project/Animations/Npc/Controller/AC_Villager.controller";
    private const string ACTION_LAYER = "Action";
    private const string IMPACT_EVENT = "OnWorkImpact";
    private const float FPS = 30f;
    private const float ARM_STRETCH = 0.93f;

    private enum Space { Root, Chest, Shoulder, Knee }

    private struct Arm
    {
        public float Weight;           // 0 keeps the idle arm
        public Space Space;
        public Vector3 Pos;            // Shoulder/Knee space: x points outward for both sides
        public Space Space2;
        public Vector3 Pos2;
        public float Blend;            // 0 = Pos, 1 = Pos2
        public Vector3 Pole;           // where the elbow points, x outward, chest frame
        public Vector3 HandFwd;        // wrist → fingers, x outward, chest frame (zero keeps the wrist)
        public Vector3 Palm;
        public float Grip;             // NaN keeps the idle fingers, 1 = fist
    }

    private struct Leg
    {
        public bool Custom;            // false plants the foot where the idle has it
        public Vector3 Pos;            // root space, absolute
        public Vector3 Pole;           // where the knee points, root space, absolute
    }

    private class Pose
    {
        public Vector3 HipsOffset;
        public float HipsHeight = float.NaN;
        public Vector3 HipsEuler;
        public Vector3 SpineEuler;
        public Vector3 HeadEuler;
        public Arm Right = Idle();
        public Arm Left = Idle();
        public Leg RightLeg;
        public Leg LeftLeg;
    }

    private class ClipDef
    {
        public string Name;
        public float Length;
        public Func<float, Pose> Sample;
        public float[] Impacts = Array.Empty<float>();
    }

    private static Animator _animator;
    private static HumanPoseHandler _handler;
    private static AnimationClip _idle;
    private static float[] _fist;
    private static string[] _muscleProps;
    private static int[] _rightFingers;
    private static int[] _leftFingers;
    private static Vector3 _armHingeR, _armHingeL, _armAxisR, _armAxisL;
    private static Quaternion _chestFrame;
    private static Vector3 _legHingeR, _legHingeL, _legAxisR, _legAxisL;

    [MenuItem("ProjectShaman/AI/Bake Villager Routine Clips")]
    public static void BakeAll()
    {
        GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH));
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        StringBuilder report = new StringBuilder("[VillagerClipBaker] rev 6\n");
        _curves.Clear();
        List<AnimationClip> baked = new List<AnimationClip>();
        try
        {
            Setup(model, report);
            foreach (ClipDef def in Definitions())
            {
                AnimationClip clip = Bake(def, report);
                AddToController(def.Name, clip, report);
                baked.Add(clip);
            }

            AssetDatabase.SaveAssets();
            CompareModels(baked, report);
        }
        finally
        {
            _handler?.Dispose();
            _handler = null;
            Object.DestroyImmediate(model);
            Debug.Log(report.ToString());
        }
    }

    // ---------- setup ----------

    private static void Setup(GameObject model, StringBuilder report)
    {
        _animator = model.GetComponent<Animator>();
        _handler = new HumanPoseHandler(_animator.avatar, model.transform);
        _idle = LoadClip(IDLE_PATH);

        _muscleProps = HumanTrait.MuscleName.Select(ToClipProperty).ToArray();
        _rightFingers = FingerMuscles("Right ");
        _leftFingers = FingerMuscles("Left ");

        AnimationClip grip = LoadClip(GRIP_PATH);
        _fist = new float[HumanTrait.MuscleCount];
        for (int i = 0; i < _fist.Length; i++)
        {
            _fist[i] = Read(grip, _muscleProps[i], 0f);
        }

        // The right hand holds the hammer in the source clip; the left fist mirrors it
        for (int i = 0; i < _leftFingers.Length; i++)
        {
            _fist[_leftFingers[i]] = _fist[_rightFingers[i]];
        }

        FindHinge(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "Right Forearm Stretch", out _armAxisR, out _armHingeR);
        FindHinge(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "Left Forearm Stretch", out _armAxisL, out _armHingeL);
        FindHinge(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "Right Lower Leg Stretch", out _legAxisR, out _legHingeR);
        FindHinge(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "Left Lower Leg Stretch", out _legAxisL, out _legHingeL);

        ApplyIdle(0f);
        report.AppendLine($"model: hips {Bone(HumanBodyBones.Hips).position.y:F3}, shoulder {Bone(HumanBodyBones.RightUpperArm).position:F3}, " +
            $"arm {Length(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm):F3}+{Length(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand):F3}, " +
            $"leg {Length(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg):F3}+{Length(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot):F3}, " +
            $"foot {Bone(HumanBodyBones.RightFoot).position:F3}, head {Bone(HumanBodyBones.Head).position.y:F3}");
    }

    // The joint bends around one axis of the parent bone; measured by moving only its stretch muscle
    private static void FindHinge(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, string muscle, out Vector3 axisLocal, out Vector3 hingeLocal)
    {
        int index = Array.IndexOf(HumanTrait.MuscleName, muscle);
        if (index < 0)
        {
            throw new InvalidOperationException($"No muscle '{muscle}'");
        }

        Transform a = Bone(upper);
        Transform b = Bone(lower);
        Transform c = Bone(end);
        Vector3 Dir(float value)
        {
            HumanPose pose = IdlePose(0f);
            pose.muscles[index] = value;
            _handler.SetHumanPose(ref pose);
            return a.InverseTransformDirection(c.position - b.position).normalized;
        }

        ApplyIdle(0f);
        axisLocal = a.InverseTransformDirection(b.position - a.position).normalized;
        Vector3 plus = Dir(1f);
        Vector3 minus = Dir(-1f);
        Vector3 bent = Vector3.Dot(plus, axisLocal) < Vector3.Dot(minus, axisLocal) ? plus : minus;
        hingeLocal = Vector3.Cross(axisLocal, bent).normalized;
    }

    // ---------- baking ----------

    private static AnimationClip Bake(ClipDef def, StringBuilder report)
    {
        int frames = Mathf.RoundToInt(def.Length * FPS);
        int muscleCount = HumanTrait.MuscleCount;
        Keyframe[][] muscleKeys = new Keyframe[muscleCount][];
        for (int m = 0; m < muscleCount; m++)
        {
            muscleKeys[m] = new Keyframe[frames + 1];
        }

        Keyframe[][] rootKeys = new Keyframe[7][];
        for (int r = 0; r < 7; r++)
        {
            rootKeys[r] = new Keyframe[frames + 1];
        }

        float worstHand = 0f, worstFoot = 0f, lowestFoot = float.MaxValue;
        string worstInfo = string.Empty;
        Quaternion previousRoot = Quaternion.identity;
        for (int f = 0; f <= frames; f++)
        {
            // The last key repeats the first so the loop closes exactly
            float time = f / FPS;
            float sampleTime = f == frames ? 0f : time;
            Pose spec = def.Sample(sampleTime);
            BuildPose(spec, sampleTime, out Vector3 rightTarget, out Vector3 leftTarget, out Vector3 rightFoot, out Vector3 leftFoot);
            float rightIk = Vector3.Distance(Bone(HumanBodyBones.RightHand).position, rightTarget);
            float leftIk = Vector3.Distance(Bone(HumanBodyBones.LeftHand).position, leftTarget);
            Vector3 rightRel = Quaternion.Inverse(_chestFrame) * (rightTarget - Bone(HumanBodyBones.RightUpperArm).position);
            Vector3 leftRel = Quaternion.Inverse(_chestFrame) * (leftTarget - Bone(HumanBodyBones.LeftUpperArm).position);

            HumanPose pose = new HumanPose { muscles = new float[muscleCount] };
            _handler.GetHumanPose(ref pose);
            ApplyGrip(pose.muscles, _rightFingers, spec.Right.Grip, sampleTime);
            ApplyGrip(pose.muscles, _leftFingers, spec.Left.Grip, sampleTime);

            // Read back through the muscles: what the clip will really show after Humanoid limits
            _handler.SetHumanPose(ref pose);
            float rightError = spec.Right.Weight > 0f ? Vector3.Distance(Bone(HumanBodyBones.RightHand).position, rightTarget) : 0f;
            float leftError = spec.Left.Weight > 0f ? Vector3.Distance(Bone(HumanBodyBones.LeftHand).position, leftTarget) : 0f;
            if (Mathf.Max(rightError, leftError) > worstHand)
            {
                bool rightWorse = rightError >= leftError;
                worstHand = Mathf.Max(rightError, leftError);
                worstInfo = $"{(rightWorse ? "R" : "L")} t={sampleTime:F2} ik={(rightWorse ? rightIk : leftIk) * 100f:F1}cm " +
                    $"target-from-shoulder {(rightWorse ? rightRel : leftRel):F2} ({(rightWorse ? rightRel : leftRel).magnitude:F2}m)";
            }

            worstFoot = Mathf.Max(worstFoot, Vector3.Distance(Bone(HumanBodyBones.RightFoot).position, rightFoot),
                Vector3.Distance(Bone(HumanBodyBones.LeftFoot).position, leftFoot));
            lowestFoot = Mathf.Min(lowestFoot, LowestPoint());

            Quaternion root = pose.bodyRotation;
            if (Quaternion.Dot(root, previousRoot) < 0f)
            {
                root = new Quaternion(-root.x, -root.y, -root.z, -root.w);
            }

            previousRoot = root;
            for (int m = 0; m < muscleCount; m++)
            {
                muscleKeys[m][f] = new Keyframe(time, pose.muscles[m]);
            }

            float[] rootValues = { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z, root.x, root.y, root.z, root.w };
            for (int r = 0; r < 7; r++)
            {
                rootKeys[r][f] = new Keyframe(time, rootValues[r]);
            }
        }

        string path = CLIP_FOLDER + def.Name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = def.Name };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.ClearCurves();
        clip.frameRate = FPS;
        for (int m = 0; m < muscleCount; m++)
        {
            SetCurve(clip, _muscleProps[m], muscleKeys[m]);
        }

        string[] rootProps = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int r = 0; r < 7; r++)
        {
            SetCurve(clip, rootProps[r], rootKeys[r]);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = false;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.heightFromFeet = false;
        settings.startTime = 0f;
        settings.stopTime = def.Length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, def.Impacts.Select(t => new AnimationEvent { time = t, functionName = IMPACT_EVENT }).ToArray());
        EditorUtility.SetDirty(clip);

        report.AppendLine($"{def.Name}: {def.Length:F1}s, hand error max {worstHand * 100f:F1}cm, foot error max {worstFoot * 100f:F1}cm, lowest point {lowestFoot * 100f:F1}cm  [{worstInfo}]");
        return clip;
    }

    private static void SetCurve(AnimationClip clip, string property, Keyframe[] keys)
    {
        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property), curve);
    }

    private static void AddToController(string stateName, AnimationClip clip, StringBuilder report)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.stateMachine.states.Any(s => s.state.name == stateName))
            {
                return;
            }
        }

        AnimatorControllerLayer action = controller.layers.First(l => l.name == ACTION_LAYER);
        int count = action.stateMachine.states.Length;
        AnimatorState state = action.stateMachine.AddState(stateName, new Vector3(600f, 60f * count, 0f));
        state.motion = clip;
        state.writeDefaultValues = action.stateMachine.states.First(s => s.state.name == "Work_Sweep").state.writeDefaultValues;
        EditorUtility.SetDirty(controller);
        report.AppendLine($"  + state '{stateName}' in {ACTION_LAYER}");
    }

    // ---------- model check ----------

    // Plays each baked clip on the reference and on the villager models: the same muscles should give the same lean and seat height
    private static void CompareModels(List<AnimationClip> clips, StringBuilder report)
    {
        report.AppendLine("lean = hips→neck from vertical (+ forward), bend = hips→spine against chest→neck, seat = hips above the lowest foot");
        foreach (string path in new[] { MODEL_PATH }.Concat(VILLAGER_MODELS))
        {
            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = model.GetComponent<Animator>();
            HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, model.transform);
            try
            {
                Transform T(HumanBodyBones bone) => animator.GetBoneTransform(bone);
                Transform chest = T(HumanBodyBones.UpperChest) != null ? T(HumanBodyBones.UpperChest) : T(HumanBodyBones.Chest);
                report.AppendLine($"  {System.IO.Path.GetFileNameWithoutExtension(path)}: spine {T(HumanBodyBones.Spine).name}, chest {(T(HumanBodyBones.Chest) != null ? T(HumanBodyBones.Chest).name : "-")}, " +
                    $"upperChest {(T(HumanBodyBones.UpperChest) != null ? T(HumanBodyBones.UpperChest).name : "-")}, scale {animator.humanScale:F3}");
                foreach (AnimationClip clip in clips)
                {
                    HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                    for (int m = 0; m < pose.muscles.Length; m++)
                    {
                        pose.muscles[m] = Read(clip, _muscleProps[m], 0f);
                    }

                    pose.bodyPosition = new Vector3(Read(clip, "RootT.x", 0f), Read(clip, "RootT.y", 0f), Read(clip, "RootT.z", 0f));
                    pose.bodyRotation = new Quaternion(Read(clip, "RootQ.x", 0f), Read(clip, "RootQ.y", 0f), Read(clip, "RootQ.z", 0f), Read(clip, "RootQ.w", 0f, 1f)).normalized;
                    handler.SetHumanPose(ref pose);

                    Vector3 hips = T(HumanBodyBones.Hips).position;
                    Vector3 trunk = T(HumanBodyBones.Neck).position - hips;
                    float lean = Vector3.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(trunk, Vector3.right), Vector3.right);
                    float bend = Vector3.Angle(T(HumanBodyBones.Spine).position - hips, T(HumanBodyBones.Neck).position - chest.position);
                    float feet = Mathf.Min(T(HumanBodyBones.LeftFoot).position.y, T(HumanBodyBones.RightFoot).position.y);
                    report.AppendLine($"    {clip.name}: lean {lean:F0}°, bend {bend:F0}°, seat {hips.y - feet:F2}m, hips {hips.y:F2}m");
                }
            }
            finally
            {
                handler.Dispose();
                Object.DestroyImmediate(model);
            }
        }
    }

    // ---------- posing ----------

    private static void BuildPose(Pose spec, float time, out Vector3 rightTarget, out Vector3 leftTarget, out Vector3 rightFootTarget, out Vector3 leftFootTarget)
    {
        ApplyIdle(time);
        Transform hips = Bone(HumanBodyBones.Hips);
        Transform rightFoot = Bone(HumanBodyBones.RightFoot);
        Transform leftFoot = Bone(HumanBodyBones.LeftFoot);
        Vector3 rightFootPos = rightFoot.position;
        Vector3 leftFootPos = leftFoot.position;
        Quaternion rightFootRot = rightFoot.rotation;
        Quaternion leftFootRot = leftFoot.rotation;
        Vector3 rightIdleHand = Bone(HumanBodyBones.RightHand).position;
        Vector3 leftIdleHand = Bone(HumanBodyBones.LeftHand).position;

        // Hips: rotate about the pelvis, then move
        Quaternion hipsRot = Quaternion.Euler(spec.HipsEuler);
        hips.rotation = hipsRot * hips.rotation;
        Vector3 offset = spec.HipsOffset;
        if (!float.IsNaN(spec.HipsHeight))
        {
            offset.y = spec.HipsHeight - hips.position.y;
        }

        hips.position += offset;

        // Spine and head bend in the frame of the bone below, so a twisted torso still nods forward
        Quaternion frame = hipsRot;
        frame = Bend(new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }, new[] { 0.4f, 0.35f, 0.25f }, spec.SpineEuler, frame);
        Quaternion chestFrame = frame;
        _chestFrame = chestFrame;
        Bend(new[] { HumanBodyBones.Neck, HumanBodyBones.Head }, new[] { 0.4f, 0.6f }, spec.HeadEuler, frame);

        // Legs first: the knee space of the hands depends on them
        rightFootTarget = spec.RightLeg.Custom ? spec.RightLeg.Pos : rightFootPos;
        leftFootTarget = spec.LeftLeg.Custom ? spec.LeftLeg.Pos : leftFootPos;
        Vector3 forward = hipsRot * Vector3.forward;
        SolveLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, _legAxisR, _legHingeR,
            rightFootTarget, spec.RightLeg.Custom ? spec.RightLeg.Pole : forward);
        SolveLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, _legAxisL, _legHingeL,
            leftFootTarget, spec.LeftLeg.Custom ? spec.LeftLeg.Pole : forward);
        if (!spec.RightLeg.Custom)
        {
            rightFoot.rotation = rightFootRot;
        }

        if (!spec.LeftLeg.Custom)
        {
            leftFoot.rotation = leftFootRot;
        }

        rightTarget = SolveArm(spec.Right, true, chestFrame, rightIdleHand);
        leftTarget = SolveArm(spec.Left, false, chestFrame, leftIdleHand);
    }

    private static Quaternion Bend(HumanBodyBones[] bones, float[] weights, Vector3 euler, Quaternion frame)
    {
        Quaternion result = frame;
        for (int i = 1; i < bones.Length; i++)
        {
            // A missing UpperChest gives its share to the bone below
            if (Bone(bones[i]) == null)
            {
                weights[i - 1] += weights[i];
            }
        }

        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = Bone(bones[i]);
            if (bone == null)
            {
                continue;
            }

            Quaternion local = Quaternion.Euler(euler * weights[i]);
            Quaternion world = result * local * Quaternion.Inverse(result);
            bone.rotation = world * bone.rotation;
            result = world * result;
        }

        return result;
    }

    private static Vector3 SolveArm(Arm arm, bool right, Quaternion chestFrame, Vector3 idleHand)
    {
        if (arm.Weight <= 0f)
        {
            return idleHand;
        }

        Vector3 target = Target(arm.Space, arm.Pos, right, chestFrame);
        if (arm.Blend > 0f)
        {
            target = Vector3.Lerp(target, Target(arm.Space2, arm.Pos2, right, chestFrame), arm.Blend);
        }

        target = Vector3.Lerp(idleHand, target, arm.Weight);

        // A fully stretched arm looks locked, so the elbow always keeps a little bend
        Vector3 shoulder = Bone(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm).position;
        float reach = ARM_STRETCH * (Length(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm, right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm) +
            Length(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm, right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand));
        target = shoulder + Vector3.ClampMagnitude(target - shoulder, reach);
        Vector3 pole = chestFrame * Mirror(arm.Pole == Vector3.zero ? new Vector3(0.6f, -1f, -0.3f) : arm.Pole, right);
        if (right)
        {
            SolveLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, _armAxisR, _armHingeR, target, pole);
        }
        else
        {
            SolveLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, _armAxisL, _armHingeL, target, pole);
        }

        if (arm.HandFwd != Vector3.zero)
        {
            AimHand(right, chestFrame * Mirror(arm.HandFwd, right), chestFrame * Mirror(arm.Palm, right), arm.Weight);
        }

        return target;
    }

    private static Vector3 Target(Space space, Vector3 pos, bool right, Quaternion chestFrame)
    {
        switch (space)
        {
            case Space.Root:
                return pos;
            case Space.Chest:
                Vector3 mid = (Bone(HumanBodyBones.RightUpperArm).position + Bone(HumanBodyBones.LeftUpperArm).position) * 0.5f;
                return mid + chestFrame * pos;
            case Space.Shoulder:
                return Bone(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm).position + chestFrame * Mirror(pos, right);
            default:
                return Bone(right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg).position + Mirror(pos, right);
        }
    }

    private static Vector3 Mirror(Vector3 v, bool right)
    {
        return right ? v : new Vector3(-v.x, v.y, v.z);
    }

    // Two-bone IK: the upper bone is turned so its hinge lies across the bend plane, then the lower bone only bends
    private static void SolveLimb(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones endBone, Vector3 axisLocal, Vector3 hingeLocal, Vector3 target, Vector3 pole)
    {
        Transform upper = Bone(upperBone);
        Transform lower = Bone(lowerBone);
        Transform end = Bone(endBone);
        Vector3 a = upper.position;
        float l1 = Vector3.Distance(a, lower.position);
        float l2 = Vector3.Distance(lower.position, end.position);
        Vector3 toTarget = target - a;
        float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.001f, (l1 + l2) * 0.999f);
        Vector3 dir = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
        if (bend.sqrMagnitude < 1e-6f)
        {
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        }

        bend.Normalize();
        float cos = Mathf.Clamp((l1 * l1 + distance * distance - l2 * l2) / (2f * l1 * distance), -1f, 1f);
        float sin = Mathf.Sqrt(1f - cos * cos);
        Vector3 joint = a + dir * (l1 * cos) + bend * (l1 * sin);
        Vector3 upperDir = (joint - a).normalized;
        Vector3 hinge = Vector3.Cross(bend, dir).normalized;
        upper.rotation = Quaternion.LookRotation(upperDir, hinge) * Quaternion.Inverse(Quaternion.LookRotation(axisLocal, hingeLocal));

        Vector3 current = end.position - lower.position;
        Vector3 wanted = a + dir * distance - lower.position;
        lower.rotation = Quaternion.FromToRotation(current, wanted) * lower.rotation;
    }

    private static void AimHand(bool right, Vector3 fwd, Vector3 palm, float weight)
    {
        Transform hand = Bone(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform middle = Bone(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        Transform index = Bone(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform little = Bone(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        Vector3 handFwd = (middle.position - hand.position).normalized;
        Vector3 side = index.position - little.position;
        Vector3 handPalm = (right ? Vector3.Cross(handFwd, side) : Vector3.Cross(side, handFwd)).normalized;
        Quaternion delta = Quaternion.LookRotation(fwd, palm) * Quaternion.Inverse(Quaternion.LookRotation(handFwd, handPalm));
        hand.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * hand.rotation;
    }

    private static void ApplyGrip(float[] muscles, int[] fingers, float grip, float time)
    {
        if (float.IsNaN(grip))
        {
            return;
        }

        foreach (int m in fingers)
        {
            muscles[m] = Mathf.Lerp(Read(_idle, _muscleProps[m], time), _fist[m], grip);
        }
    }

    // ---------- idle source ----------

    private static void ApplyIdle(float time)
    {
        HumanPose pose = IdlePose(time);
        _handler.SetHumanPose(ref pose);
    }

    private static HumanPose IdlePose(float time)
    {
        float t = time % _idle.length;
        HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int m = 0; m < pose.muscles.Length; m++)
        {
            pose.muscles[m] = Read(_idle, _muscleProps[m], t);
        }

        pose.bodyPosition = new Vector3(Read(_idle, "RootT.x", t), Read(_idle, "RootT.y", t), Read(_idle, "RootT.z", t));
        pose.bodyRotation = new Quaternion(Read(_idle, "RootQ.x", t), Read(_idle, "RootQ.y", t), Read(_idle, "RootQ.z", t), Read(_idle, "RootQ.w", t, 1f)).normalized;
        return pose;
    }

    private static readonly Dictionary<(AnimationClip, string), AnimationCurve> _curves = new Dictionary<(AnimationClip, string), AnimationCurve>();

    private static float Read(AnimationClip clip, string property, float time, float fallback = 0f)
    {
        if (!_curves.TryGetValue((clip, property), out AnimationCurve curve))
        {
            curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property));
            _curves[(clip, property)] = curve;
        }

        return curve != null ? curve.Evaluate(time) : fallback;
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }

    // Finger muscles are named "Right Index 1 Stretched" but the clip property is "RightHand.Index.1 Stretched"
    private static string ToClipProperty(string muscle)
    {
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        foreach (string side in new[] { "Left", "Right" })
        {
            foreach (string finger in fingers)
            {
                string prefix = $"{side} {finger} ";
                if (muscle.StartsWith(prefix))
                {
                    return $"{side}Hand.{finger}.{muscle.Substring(prefix.Length)}";
                }
            }
        }

        return muscle;
    }

    private static int[] FingerMuscles(string side)
    {
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        return Enumerable.Range(0, HumanTrait.MuscleCount)
            .Where(i => fingers.Any(f => HumanTrait.MuscleName[i].StartsWith(side + f + " ")))
            .ToArray();
    }

    private static Transform Bone(HumanBodyBones bone)
    {
        return _animator.GetBoneTransform(bone);
    }

    private static float Length(HumanBodyBones a, HumanBodyBones b)
    {
        return Vector3.Distance(Bone(a).position, Bone(b).position);
    }

    private static float LowestPoint()
    {
        float lowest = float.MaxValue;
        foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot, HumanBodyBones.RightToes, HumanBodyBones.LeftToes,
                     HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
        {
            Transform t = Bone(bone);
            if (t != null)
            {
                lowest = Mathf.Min(lowest, t.position.y);
            }
        }

        return lowest;
    }

    // ---------- key helpers ----------

    private static Arm Idle()
    {
        return new Arm { Grip = float.NaN };
    }

    private static Arm Reach(Space space, Vector3 pos, float grip = float.NaN, Vector3 pole = default)
    {
        return new Arm { Weight = 1f, Space = space, Pos = pos, Pole = pole, Grip = grip };
    }

    private static Arm Aim(this Arm arm, Vector3 fwd, Vector3 palm)
    {
        arm.HandFwd = fwd;
        arm.Palm = palm;
        return arm;
    }

    private static Arm Weighted(this Arm arm, float weight)
    {
        arm.Weight = weight;
        return arm;
    }

    // Looping Catmull-Rom through (time, value) keys; the first key is repeated at the clip length
    private static Vector3 Keys(float t, float length, params (float time, Vector3 value)[] keys)
    {
        int n = keys.Length;
        int i = n - 1;
        for (int k = 0; k < n; k++)
        {
            if (keys[k].time > t)
            {
                i = k - 1;
                break;
            }
        }

        if (i < 0)
        {
            i = n - 1;
        }

        int next = (i + 1) % n;
        float start = keys[i].time;
        float end = next == 0 ? length + keys[0].time : keys[next].time;
        float local = t < start ? t + length : t;
        float u = end > start ? (local - start) / (end - start) : 0f;
        Vector3 p0 = keys[(i - 1 + n) % n].value;
        Vector3 p1 = keys[i].value;
        Vector3 p2 = keys[next].value;
        Vector3 p3 = keys[(next + 1) % n].value;
        float u2 = u * u;
        float u3 = u2 * u;
        return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
    }

    private static float Keys(float t, float length, params (float time, float value)[] keys)
    {
        return Keys(t, length, keys.Select(k => (k.time, new Vector3(k.value, 0f, 0f))).ToArray()).x;
    }

    private static float Wave(float t, float period, float phase = 0f)
    {
        return Mathf.Sin((t / period + phase) * Mathf.PI * 2f);
    }

    private static Vector3 V(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    // ---------- clips ----------

    // Cross-legged on the floor: each foot tucked under the other knee, knees out
    private const float SIT_HIPS = 0.14f;

    private static void SitLegs(Pose pose)
    {
        pose.HipsHeight = SIT_HIPS;
        pose.HipsOffset = V(0f, 0f, -0.05f);
        pose.RightLeg = new Leg { Custom = true, Pos = V(-0.16f, 0.07f, 0.30f), Pole = V(1f, 0.25f, 0.6f) };
        pose.LeftLeg = new Leg { Custom = true, Pos = V(0.17f, 0.06f, 0.16f), Pole = V(-1f, 0.25f, 0.6f) };
    }

    private static IEnumerable<ClipDef> Definitions()
    {
        // 마당 쓸기·주막 허드렛일 (Broom): bent over, both hands on the handle, fast stroke to the left and a slow lift back
        yield return new ClipDef
        {
            Name = "Work_Sweep",
            Length = 1.6f,
            Impacts = new[] { 0.35f },
            Sample = t =>
            {
                const float L = 1.6f;
                Vector3 stroke = Keys(t, L, (0f, V(0.10f, 0f, 0f)), (0.2f, V(0.06f, -0.02f, 0.02f)), (0.5f, V(-0.10f, -0.01f, 0.01f)),
                    (0.75f, V(-0.11f, 0.03f, -0.01f)), (1.2f, V(0.02f, 0.04f, -0.01f)));
                return new Pose
                {
                    HipsOffset = V(0f, -0.06f, -0.03f),
                    HipsEuler = V(8f, stroke.x * 60f, 0f),
                    SpineEuler = V(18f, stroke.x * 70f, 0f),
                    HeadEuler = V(10f, -stroke.x * 60f, 0f),
                    Right = Reach(Space.Chest, V(0.14f + stroke.x, -0.32f + stroke.y, 0.18f + stroke.z), 1f, V(0.6f, -1f, -0.2f)),
                    Left = Reach(Space.Chest, V(0.04f + stroke.x * 0.6f, -0.12f + stroke.y * 0.5f, 0.20f), 1f, V(0.8f, -1f, -0.1f)),
                };
            },
        };

        // 씨 뿌리기: left hand holds the seed pouch at the hip, the right reaches in and casts in an arc to the right
        yield return new ClipDef
        {
            Name = "Work_Sow",
            Length = 2.4f,
            Impacts = new[] { 1.25f },
            Sample = t =>
            {
                const float L = 2.4f;
                Vector3 hand = Keys(t, L, (0f, V(-0.24f, -0.40f, 0.26f)), (0.5f, V(-0.18f, -0.34f, 0.30f)), (0.95f, V(-0.02f, -0.18f, 0.36f)),
                    (1.25f, V(0.36f, -0.02f, 0.34f)), (1.6f, V(0.30f, -0.34f, 0.18f)), (2.0f, V(-0.12f, -0.42f, 0.22f)));
                float grip = Keys(t, L, (0f, 0.1f), (0.35f, 1f), (1.1f, 1f), (1.3f, 0f), (2.0f, 0f));
                float twist = Keys(t, L, (0f, -12f), (0.5f, -10f), (1.25f, 18f), (1.7f, 10f), (2.1f, -8f));
                return new Pose
                {
                    HipsEuler = V(0f, twist * 0.3f, 0f),
                    SpineEuler = V(10f, twist * 0.7f, 0f),
                    HeadEuler = V(5f, twist * 0.6f, 0f),
                    Right = Reach(Space.Shoulder, hand * 0.8f, grip),
                    Left = Reach(Space.Shoulder, V(-0.02f, -0.33f, 0.16f) + V(0f, 0.015f * Wave(t, L), 0f), 0.8f, V(1f, -1f, -0.3f)),
                };
            },
        };

        // 그물 손질: sitting cross-legged, the left hand holds the net on the lap, the right ties a knot and pulls it tight
        yield return new ClipDef
        {
            Name = "Work_NetMend",
            Length = 2.0f,
            Impacts = new[] { 1.55f },
            Sample = t =>
            {
                const float L = 2.0f;
                float loop = Mathf.Clamp01(t / 1.2f) * Mathf.PI * 2f;
                Vector3 knot = t < 1.2f ? V(Mathf.Sin(loop) * 0.04f, (1f - Mathf.Cos(loop)) * 0.03f, 0f) : Vector3.zero;
                Vector3 pull = Keys(t, L, (0f, Vector3.zero), (1.2f, Vector3.zero), (1.55f, V(0.12f, 0.08f, -0.02f)), (1.8f, V(0.03f, 0.02f, 0f)));
                Pose pose = new Pose
                {
                    HipsEuler = V(6f, 0f, 0f),
                    SpineEuler = V(12f + 2f * Wave(t, L), 0f, 0f),
                    HeadEuler = V(22f, 4f * Wave(t, L, 0.25f), 0f),
                    Right = Reach(Space.Chest, V(0.08f, -0.30f, 0.23f) + knot + pull, 0.7f, V(0.8f, -1f, -0.2f)),
                    Left = Reach(Space.Chest, V(-0.09f, -0.31f, 0.22f) - pull * 0.3f, 0.85f, V(0.8f, -1f, -0.2f)),
                };
                SitLegs(pose);
                return pose;
            },
        };

        // 호객 행위 (upper body): calls with a hand cupped at the mouth, then waves people in, the other hand on the hip
        yield return new ClipDef
        {
            Name = "Work_Hawk",
            Length = 3.6f,
            Impacts = new[] { 0.9f },
            Sample = t =>
            {
                const float L = 3.6f;
                bool calling = t < 1.6f;
                float beckon = t >= 1.8f && t < 3.0f ? 0.5f - 0.5f * Mathf.Cos((t - 1.8f) / 0.6f * Mathf.PI * 2f) : 0f;
                Vector3 hand = calling ? V(-0.12f, 0.10f, 0.17f) : V(0.08f, 0.00f, 0.38f) + V(-0.02f, 0.05f, -0.14f) * beckon;
                float weight = Keys(t, L, (0f, 0f), (0.35f, 1f), (3.0f, 1f), (3.45f, 0f));
                if (t >= 1.5f && t < 1.8f)
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - 1.5f) / 0.3f);
                    hand = Vector3.Lerp(V(-0.12f, 0.10f, 0.17f), V(0.08f, 0.00f, 0.38f), u);
                }

                float lean = Keys(t, L, (0f, 0f), (0.5f, -6f), (1.4f, -6f), (2.0f, 4f), (3.2f, 2f));
                Arm right = Reach(Space.Shoulder, hand, calling ? 0.35f : 0.2f, V(0.8f, -0.6f, -0.2f)).Weighted(weight);
                right = calling ? right.Aim(V(-0.3f, 1f, 0.2f), V(-0.2f, 0f, 1f)) : right.Aim(V(0f, 0.5f, 1f), V(0f, 1f, -0.4f));
                return new Pose
                {
                    SpineEuler = V(lean, calling ? -8f * Wave(t, 1.6f) : 12f, 0f),
                    HeadEuler = V(calling ? -10f : 0f, calling ? -15f * Wave(t, 1.6f) : 15f, 0f),
                    Right = right,
                    Left = Reach(Space.Shoulder, V(0.16f, -0.40f, -0.04f), 0.6f, V(1f, -0.2f, -0.6f)),
                };
            },
        };

        // 흥정하기 (upper body): offers with an open palm, raises a hand and shakes the head, then nods
        yield return new ClipDef
        {
            Name = "Work_Bargain",
            Length = 4.0f,
            Impacts = new[] { 3.5f },
            Sample = t =>
            {
                const float L = 4.0f;
                float rightWeight = Keys(t, L, (0f, 0f), (0.4f, 1f), (1.5f, 1f), (2.0f, 0.15f), (3.4f, 0f));
                Vector3 offer = Keys(t, L, (0f, V(0.02f, -0.24f, 0.32f)), (0.9f, V(0.08f, -0.18f, 0.38f)), (1.4f, V(0.00f, -0.24f, 0.33f)), (2.5f, V(0.02f, -0.30f, 0.28f)));
                float leftWeight = Keys(t, L, (0f, 0f), (1.6f, 0f), (2.0f, 1f), (2.8f, 1f), (3.3f, 0f));
                float wave = t >= 2.0f && t < 2.8f ? Mathf.Sin((t - 2.0f) * Mathf.PI * 2f * 2.5f) : 0f;
                float shake = t >= 1.9f && t < 2.9f ? 14f * Mathf.Sin((t - 1.9f) * Mathf.PI * 2f * 2.5f) : 0f;
                float nod = t >= 3.2f && t < 3.9f ? 10f * Mathf.Sin((t - 3.2f) / 0.35f * Mathf.PI) : 0f;
                return new Pose
                {
                    HipsOffset = V(0.02f * Wave(t, L), 0f, 0f),
                    SpineEuler = V(4f + nod * 0.2f, 6f * Wave(t, L, 0.1f), 0f),
                    HeadEuler = V(nod, shake, 0f),
                    Right = Reach(Space.Shoulder, offer, 0.15f).Aim(V(0.1f, 0.3f, 1f), V(0f, 1f, 0f)).Weighted(rightWeight),
                    Left = Reach(Space.Shoulder, V(-0.04f + 0.05f * wave, -0.08f, 0.30f), 0f, V(1f, -1f, 0f)).Aim(V(0f, 1f, 0.2f), V(0f, 0.1f, 1f)).Weighted(leftWeight),
                };
            },
        };

        // 장부 정리 (upper body): the book open on the left forearm, the right hand writes, then turns a page
        yield return new ClipDef
        {
            Name = "Work_Ledger",
            Length = 4.2f,
            Impacts = new[] { 3.2f },
            Sample = t =>
            {
                const float L = 4.2f;
                Vector3 writing = V(-0.10f, -0.24f, 0.33f);
                Vector3 hand = t < 2.6f
                    ? writing + V(0.03f * Mathf.Sin(t * Mathf.PI * 2f * 1.5f), 0.008f * Mathf.Sin(t * Mathf.PI * 2f * 3f) - 0.015f * (t / 2.6f), 0f)
                    : Keys(t, L, (0f, writing), (2.6f, writing + V(0f, -0.015f, 0f)), (2.9f, V(-0.16f, -0.17f, 0.36f)), (3.2f, V(-0.26f, -0.18f, 0.26f)), (3.7f, writing));
                return new Pose
                {
                    SpineEuler = V(8f, 0f, 0f),
                    HeadEuler = V(26f, t > 2.8f && t < 3.4f ? -8f : 3f * Mathf.Sin(t * Mathf.PI * 2f * 0.7f), 0f),
                    Right = Reach(Space.Shoulder, hand, 0.7f, V(0.8f, -1f, -0.2f)).Aim(V(-0.4f, -0.4f, 1f), V(-0.3f, -1f, 0f)),
                    Left = Reach(Space.Shoulder, V(-0.12f, -0.28f, 0.30f), 0.3f, V(1f, -1f, -0.1f)).Aim(V(-1f, 0.1f, 0.4f), V(0f, 1f, 0.2f)),
                };
            },
        };

        // 다듬이질·빨래하기 (Pound): sitting on the floor before the stone, the left hand keeps the cloth flat, the right beats it
        // with the bat in a quick wrist-led rhythm; the body dips a little into each blow
        yield return new ClipDef
        {
            Name = "Work_Pound",
            Length = 0.9f,
            Impacts = new[] { 0.3f },
            Sample = t =>
            {
                const float L = 0.9f;
                Vector3 hand = Keys(t, L, (0f, V(0.00f, -0.12f, 0.24f)), (0.22f, V(-0.04f, -0.28f, 0.28f)), (0.3f, V(-0.05f, -0.34f, 0.28f)),
                    (0.4f, V(-0.05f, -0.31f, 0.28f)), (0.65f, V(-0.02f, -0.18f, 0.26f)));
                // The bat leaves the fist on the thumb side: with the palm turned inward the fingers point 90° below it
                Vector3 bat = Keys(t, L, (0f, V(0f, 1f, -0.3f)), (0.22f, V(0f, 0.3f, 1f)), (0.3f, V(0f, -0.4f, 1f)), (0.4f, V(0f, -0.3f, 1f)), (0.65f, V(0f, 0.7f, 0.5f)));
                float dip = Keys(t, L, (0f, 0f), (0.3f, 1f), (0.5f, 0.4f), (0.75f, 0f));
                Pose pose = new Pose
                {
                    HipsEuler = V(6f, 0f, 0f),
                    SpineEuler = V(8f + 4f * dip, 6f, 0f),
                    HeadEuler = V(20f, -6f, 0f),
                    Right = Reach(Space.Shoulder, hand, 1f, V(1f, -0.6f, -0.4f)).Aim(V(0f, -bat.z, bat.y), V(-1f, 0f, 0f)),
                    Left = Reach(Space.Shoulder, V(-0.14f, -0.34f, 0.24f) + V(0f, 0.01f * dip, 0f), 0.2f, V(1f, -1f, -0.2f)).Aim(V(-0.3f, -0.2f, 1f), V(0f, -1f, 0f)),
                };
                SitLegs(pose);
                return pose;
            },
        };

        // 그늘·정자에서 쉬기: cross-legged, hands on the knees, slow breathing, looking around
        yield return new ClipDef
        {
            Name = "Rest_Sit",
            Length = 6.0f,
            Sample = t =>
            {
                const float L = 6.0f;
                float look = Keys(t, L, (0f, 0f), (1.5f, 22f), (2.7f, 18f), (4.0f, -15f), (5.2f, -10f));
                Pose pose = new Pose
                {
                    HipsEuler = V(6f, 0f, 0f),
                    SpineEuler = V(2f + 1.5f * Wave(t, 3f), look * 0.2f, 0f),
                    HeadEuler = V(2f * Wave(t, 3f, 0.2f), look, 0f),
                    Right = Reach(Space.Knee, V(-0.20f, 0.11f, -0.26f), float.NaN, V(1f, -0.3f, -0.5f)).Aim(V(0.2f, -0.2f, 1f), V(0f, -1f, 0f)),
                    Left = Reach(Space.Knee, V(-0.20f, 0.11f, -0.26f), float.NaN, V(1f, -0.3f, -0.5f)).Aim(V(0.2f, -0.2f, 1f), V(0f, -1f, 0f)),
                };
                SitLegs(pose);
                return pose;
            },
        };

        // 주막에서 쉬기: cross-legged, lifts a bowl to the mouth, drinks with the head tipped back, puts it down on the knee
        yield return new ClipDef
        {
            Name = "Rest_Drink",
            Length = 5.0f,
            Sample = t =>
            {
                const float L = 5.0f;
                float lift = Keys(t, L, (0f, 0f), (1.0f, 0f), (1.8f, 1f), (3.2f, 1f), (4.0f, 0f));
                float tip = Keys(t, L, (0f, 0f), (1.9f, 0f), (2.4f, -16f), (2.9f, -16f), (3.3f, 0f));
                Arm right = Reach(Space.Knee, V(-0.20f, 0.13f, -0.24f), 0.55f, V(1f, -0.6f, -0.3f));
                right.Space2 = Space.Shoulder;
                right.Pos2 = V(-0.13f, 0.02f, 0.22f) + V(0f, -tip * 0.002f, 0f);
                right.Blend = lift;
                right = right.Aim(V(-0.6f, 0.4f, 0.6f), V(-0.5f, 0.8f, 0f));
                Pose pose = new Pose
                {
                    HipsEuler = V(6f, 0f, 0f),
                    SpineEuler = V(2f + tip * 0.5f, 0f, 0f),
                    HeadEuler = V(tip, 0f, 0f),
                    Right = right,
                    Left = Reach(Space.Knee, V(-0.20f, 0.11f, -0.26f), float.NaN, V(1f, -0.3f, -0.5f)).Aim(V(0.2f, -0.2f, 1f), V(0f, -1f, 0f)),
                };
                SitLegs(pose);
                return pose;
            },
        };

        // 아이들 놀이: a clapping game, bouncing on the knees, hands up at the end of each round
        yield return new ClipDef
        {
            Name = "Rest_Play",
            Length = 1.6f,
            Sample = t =>
            {
                const float L = 1.6f;
                Vector3 open = V(0.10f, -0.20f, 0.30f);
                Vector3 clap = V(-0.15f, -0.20f, 0.32f);
                Vector3 up = V(0.08f, 0.32f, 0.10f);
                Vector3 hand = Keys(t, L, (0f, open), (0.2f, clap), (0.4f, open), (0.6f, clap), (0.8f, open), (1.1f, up), (1.35f, up));
                float bounce = Mathf.Abs(Mathf.Sin(t / L * Mathf.PI * 4f));
                return new Pose
                {
                    HipsOffset = V(0f, -0.05f * bounce, 0f),
                    SpineEuler = V(5f - 4f * bounce, 0f, 0f),
                    HeadEuler = V(-4f, 10f * Wave(t, L), 0f),
                    Right = Reach(Space.Shoulder, hand, 0f, V(1f, -1f, -0.3f)).Aim(V(-0.2f, 1f, 0.3f), V(-1f, 0f, 0f)),
                    Left = Reach(Space.Shoulder, hand, 0f, V(1f, -1f, -0.3f)).Aim(V(-0.2f, 1f, 0.3f), V(-1f, 0f, 0f)),
                };
            },
        };

        // 우물가 수다: standing, a hand gesture while talking, laughs behind the other hand, nods along
        yield return new ClipDef
        {
            Name = "Rest_Chat",
            Length = 4.5f,
            Sample = t =>
            {
                const float L = 4.5f;
                float talk = Keys(t, L, (0f, 0f), (0.5f, 1f), (1.4f, 1f), (1.9f, 0f), (3.0f, 0f));
                Vector3 gesture = Keys(t, L, (0f, V(0.04f, -0.26f, 0.30f)), (0.7f, V(0.06f, -0.20f, 0.36f)), (1.1f, V(0.16f, -0.22f, 0.32f)), (1.6f, V(0.06f, -0.30f, 0.26f)));
                float laugh = Keys(t, L, (0f, 0f), (2.0f, 0f), (2.4f, 1f), (3.2f, 1f), (3.6f, 0f));
                float chuckle = t > 2.4f && t < 3.2f ? 4f * Mathf.Sin((t - 2.4f) * Mathf.PI * 2f * 5f) : 0f;
                float nod = t > 3.6f && t < 4.3f ? 9f * Mathf.Sin((t - 3.6f) / 0.35f * Mathf.PI) : 0f;
                return new Pose
                {
                    HipsOffset = V(0.025f * Wave(t, L), 0f, 0f),
                    HipsEuler = V(0f, 0f, -2f * Wave(t, L)),
                    SpineEuler = V(3f + laugh * 6f + chuckle, 4f * Wave(t, L, 0.3f), 0f),
                    HeadEuler = V(nod - laugh * 4f, 8f * Wave(t, L, 0.15f), 0f),
                    Right = Reach(Space.Shoulder, gesture, 0.1f).Aim(V(0.1f, 0.2f, 1f), V(-0.2f, 1f, 0f)).Weighted(talk),
                    Left = Reach(Space.Shoulder, V(-0.13f, 0.06f, 0.18f), 0.4f, V(1f, -1f, 0f)).Aim(V(-0.4f, 1f, 0.3f), V(0f, 0f, -1f)).Weighted(laugh),
                };
            },
        };
    }
}
