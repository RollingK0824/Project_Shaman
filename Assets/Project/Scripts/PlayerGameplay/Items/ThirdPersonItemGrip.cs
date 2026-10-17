using System;
using UnityEngine;

// Presentation only: the world character's fingers. Does not touch the view model or item socket.
[DefaultExecutionOrder(80)]
public sealed class ThirdPersonItemGrip : MonoBehaviour
{
    private Animator _animator;
    private HumanPoseHandler _handler;
    private HumanPose _pose;
    private Transform[] _bones;
    private Vector3[] _positions;
    private Quaternion[] _rotations;
    private readonly Transform[] _fingers = new Transform[15];
    private readonly Quaternion[] _base = new Quaternion[15];
    private readonly Quaternion[] _posed = new Quaternion[15];
    private readonly Vector3[] _tipOffsets = new Vector3[5];
    private readonly int[] _muscles = new int[20];
    private readonly float[] _values = new float[20];
    private float _weight;
    private bool _applied, _pinch, _contactGrip;
    private Vector3 _contact;

    public void Bind(Animator animator, ItemData data, Bounds bounds)
    {
        if (animator == null || !animator.isHuman || !animator.avatar.isValid) return;
        _animator = animator;
        _handler = new HumanPoseHandler(animator.avatar, animator.transform);
        _bones = animator.GetComponentsInChildren<Transform>(true);
        _positions = new Vector3[_bones.Length];
        _rotations = new Quaternion[_bones.Length];
        string[] names = { "Thumb", "Index", "Middle", "Ring", "Little" };
        string[] joints = { "Proximal", "Intermediate", "Distal" };
        for (int f = 0; f < 5; f++)
        {
            for (int j = 0; j < 3; j++)
                _fingers[f * 3 + j] = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), "Right" + names[f] + joints[j]));
            for (int j = 0; j < 4; j++)
                _muscles[f * 4 + j] = Array.IndexOf(HumanTrait.MuscleName, "Right " + names[f] + (j == 3 ? " Spread" : " " + (j + 1) + " Stretched"));
        }
        bool paper = data is TalismanItemData;
        _pinch = paper || data.UseAction is CoinUseAction;
        bool palm = data.HoldType == ProjectShaman.ItemHold.HoldType.OneHandPalm;
        // Relax unused fingers; cylindrical handles retain a full, graduated grasp.
        for (int f = 0; f < 5; f++)
        {
            _values[f * 4] = palm ? .15f : _pinch && f > 1 ? -.4f : -.25f;
            _values[f * 4 + 1] = palm ? .05f : _pinch && f > 1 ? -.7f : -.55f;
            _values[f * 4 + 2] = palm ? .2f : _pinch && f > 1 ? -.35f : -.3f;
            _values[f * 4 + 3] = 0f;
        }
        _values[0] = _pinch || palm ? .25f : -.2f;
        _values[1] = _pinch || palm ? .2f : -.35f;
        _values[2] = _pinch || palm ? .15f : -.1f; _values[3] = -.25f;
        if (_pinch) { _values[4] = .25f; _values[5] = -.4f; _values[6] = .1f; }
        bool bag = data.UseAction is SaltUseAction || data.UseAction is BeanMugwortUseAction;
        if (bag)
        {
            for (int f = 1; f < 5; f++) { _values[f*4] = -.05f; _values[f*4+1] = -.4f; _values[f*4+2] = -.2f; }
        }
        _contact = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .12f, bounds.center.z);
        if (bag) _contact.y = bounds.min.y + bounds.size.y * .8f;
        if (data.UseAction is SaltUseAction) _contact.x = bounds.min.x + bounds.size.x * .15f;
        _contactGrip = _pinch || bag;
    }

    private void Update() => Restore(); // Prevent accumulation if Animator culls or pauses.
    private void LateUpdate()
    {
        if (_handler == null || _animator == null || !_animator.isActiveAndEnabled) return;
        _weight = Mathf.MoveTowards(_weight, 1f, Time.deltaTime / .22f);
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) { _positions[i] = _bones[i].localPosition; _rotations[i] = _bones[i].localRotation; }
        for (int i = 0; i < 15; i++) if (_fingers[i] != null) _base[i] = _fingers[i].localRotation;
        _handler.GetHumanPose(ref _pose);
        for (int i = 0; i < 20; i++) if (_muscles[i] >= 0) _pose.muscles[_muscles[i]] = _values[i];
        _handler.SetHumanPose(ref _pose);
        for (int i = 0; i < 15; i++) if (_fingers[i] != null) _posed[i] = _fingers[i].localRotation;
        // HumanPoseHandler also writes root/body transforms. Restore those exactly.
        for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) { _bones[i].localPosition = _positions[i]; _bones[i].localRotation = _rotations[i]; }
        for (int i = 0; i < 15; i++) if (_fingers[i] != null) _fingers[i].localRotation = _posed[i];
        if (_contactGrip)
        {
            Vector3 point = transform.TransformPoint(_contact);
            Vector3 normal = transform.forward;
            // Pads straddle the paper/coin rather than piercing its centre.
            SolveFinger(0, point - normal * .006f);
            SolveFinger(3, point + normal * .006f);
        }
        for (int i = 0; i < 15; i++) if (_fingers[i] != null)
            _fingers[i].localRotation = Quaternion.Slerp(_base[i], _fingers[i].localRotation, _weight);
        _applied = true;
    }
    private void SolveFinger(int first, Vector3 target)
    {
        Transform middle = _fingers[first+1], tip = _fingers[first+2];
        if (_fingers[first] == null || middle == null || tip == null) return;
        Vector3 tipOffset = tip.InverseTransformVector((tip.position-middle.position) * .8f);
        if (tip.childCount > 0) tipOffset = tip.GetChild(0).localPosition;
        _tipOffsets[first/3] = tipOffset;
        for (int pass = 0; pass < 6; pass++)
            for (int j = 2; j >= 0; j--)
            {
                Transform joint = _fingers[first+j];
                Quaternion delta = Quaternion.FromToRotation(tip.TransformPoint(tipOffset)-joint.position, target-joint.position);
                joint.rotation = Quaternion.RotateTowards(Quaternion.identity, delta, 18f) * joint.rotation;
                joint.localRotation = Quaternion.RotateTowards(_posed[first+j], joint.localRotation, 50f);
            }
    }
    private void Restore()
    {
        if (!_applied) return;
        for (int i = 0; i < 15; i++) if (_fingers[i] != null) _fingers[i].localRotation = _base[i];
        _applied = false;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() { Restore(); _handler?.Dispose(); }
}
