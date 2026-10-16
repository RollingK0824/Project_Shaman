using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BeanMugwortProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField]
    private float _lifeTime = 6f;

    [SerializeField]
    private float _destroyDelayAfterHit = 0.1f;

    [Header("Throw")]
    [SerializeField]
    private float _randomTorque = 4f;

    [SerializeField] private LayerMask _targetMask = ~0;

    private Rigidbody _rigidbody;
    private GameObject _owner;
    private bool _hasHit;
    private bool _visualOnly;
    private ItemAttackSession _attack;


    private void Awake()
    {
        _rigidbody =
            GetComponent<Rigidbody>();
    }


    public void Initialize(
        GameObject owner,
        Vector3 direction,
        float throwForce,
        float slowMultiplier,
        float slowDuration)
    {
        _visualOnly = false;
        _owner = owner;
        _hasHit = false;
        _attack?.End();
        _attack = new ItemAttackSession(owner, 0f,
            new ItemControlEffect { Kind = ItemControlKind.Slow, Duration = slowDuration, SpeedMultiplier = slowMultiplier },
            ItemReactionFallback.BeanMugwort, _targetMask, _lifeTime);

        SetVisible(false);
        Launch(direction, throwForce);

        // 아래는 Launch()로 이동
        //GetComponent<Collider>().isTrigger = true;
        //
        //IgnoreOwnerCollision();
        //
        //_rigidbody.AddForce(
        //    direction.normalized * throwForce,
        //    ForceMode.VelocityChange
        //);
        //
        //_rigidbody.AddTorque(
        //    Random.insideUnitSphere *
        //    _randomTorque,
        //    ForceMode.VelocityChange
        //);
        //
        //Destroy(
        //    gameObject,
        //    _lifeTime
        //);
    }

    public void InitializeVisual(GameObject owner, Vector3 direction, float throwForce)
    {
        _visualOnly = true;
        _owner = owner;
        _hasHit = false;
        _attack?.End();
        _attack = null;

        Launch(direction, throwForce);
    }

    private void Launch(Vector3 direction, float throwForce)
    {
        GetComponent<Collider>().isTrigger = true;

        IgnoreOwnerCollision();

        _rigidbody.AddForce(direction.normalized * throwForce, ForceMode.VelocityChange);

        _rigidbody.AddTorque(Random.insideUnitSphere * _randomTorque, ForceMode.VelocityChange);

        Destroy(gameObject, _lifeTime);
    }
    private void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        if (visible) return;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }


    private void OnTriggerEnter(Collider other) => Hit(other);

    // Compatibility for existing solid-collider variants: both paths use the same attack ledger.
    private void OnCollisionEnter(Collision collision) => Hit(collision.collider);

    private void Hit(Collider other)
    {
        //if (!isActiveAndEnabled || _hasHit || _attack == null || other == null || (_owner != null && other.transform.IsChildOf(_owner.transform))) return;

        if (!isActiveAndEnabled || _hasHit || other == null ||
            (_owner != null && other.transform.IsChildOf(_owner.transform))) return;

        bool reacted = false;
        if (_visualOnly)
        {
            if (other.isTrigger) return;
        }
        else
        {
            if (_attack == null) return;
            reacted = _attack.TryHit(other);
            // Unrelated sensor volumes (stress zones etc.) must not swallow the thrown item.
            if (other.isTrigger && !reacted) return;
            _attack.End();
        }


        _hasHit = true;
        GetComponent<Collider>().enabled = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;

        if (!_visualOnly)
        {
            Debug.Log(reacted ? "[BeanMugwort] 대상 적중 → 둔화 반응 요청" : $"[BeanMugwort] 투척물 충돌: {other.name}");
        }
       
        Destroy(gameObject, _destroyDelayAfterHit);
    }

    private void OnDisable() => _attack?.End();

    private void IgnoreOwnerCollision()
    {
        if (_owner == null)
        {
            return;
        }

        Collider projectileCollider =
            GetComponent<Collider>();

        Collider[] ownerColliders =
            _owner.GetComponentsInChildren<Collider>(true);

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(
                projectileCollider,
                ownerCollider,
                true
            );
        }
    }
}