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
    private readonly RaycastHit[] _sweepHits = new RaycastHit[64];
    public float FlightLifetime => _lifeTime;
    public float CollisionRadius
    {
        get
        {
            var capsule = GetComponent<CapsuleCollider>();
            var scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return capsule != null ? Mathf.Max(capsule.radius, capsule.height * .5f) * maxScale : .2f;
        }
    }

    public static bool BlocksFlight(Collider other, GameObject owner)
    {
        return other != null && !other.isTrigger &&
            (owner == null || !other.transform.IsChildOf(owner.transform)) &&
            other.GetComponentInParent<BeanMugwortProjectile>() == null && !SaltUseAction.CanHitGhost(other);
    }


    private void Awake()
    {
        _rigidbody =
            GetComponent<Rigidbody>();
    }


    public void Initialize(
        GameObject owner,
        Vector3 direction,
        float throwForce,
        float damageAmount)
    {
        _visualOnly = false;
        _owner = owner;
        _hasHit = false;
        _attack?.End();
        // Preserve the former salt reaction for HP-less ghosts; never hurt players or villagers.
        _attack = new ItemAttackSession(owner, damageAmount, default,
            ItemReactionFallback.Salt, _targetMask, _lifeTime, SaltUseAction.CanHitGhost);

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

        ItemVfxPresentation.Trail(transform);
        Launch(direction, throwForce);
    }

    private void Launch(Vector3 direction, float throwForce)
    {
        GetComponent<Collider>().isTrigger = true;

        IgnoreOwnerCollision();

        _rigidbody.linearVelocity = direction.normalized * throwForce;

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

    private void FixedUpdate()
    {
        if (_hasHit || _owner == null || _rigidbody.isKinematic) return;
        var velocity = _rigidbody.linearVelocity + (_rigidbody.useGravity ? Physics.gravity * Time.fixedDeltaTime : Vector3.zero);
        Vector3 delta = velocity * Time.fixedDeltaTime;
        if (delta.sqrMagnitude < .000001f) return;
        int count = Physics.SphereCastNonAlloc(_rigidbody.position, CollisionRadius, delta.normalized,
            _sweepHits, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
        // Resolve in travel order: a wall stops this step before targets beyond it.
        System.Array.Sort(_sweepHits, 0, count, FlightHitComparer.Instance);
        for (int i = 0; i < count && !_hasHit; i++)
        {
            if (BlocksFlight(_sweepHits[i].collider, _owner))
                _rigidbody.position += delta.normalized * _sweepHits[i].distance;
            Hit(_sweepHits[i].collider);
        }
    }

    private sealed class FlightHitComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly FlightHitComparer Instance = new FlightHitComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // Compatibility for existing solid-collider variants: both paths use the same attack ledger.
    private void OnCollisionEnter(Collision collision) => Hit(collision.collider);

    private void Hit(Collider other)
    {
        if (!isActiveAndEnabled || _hasHit || other == null ||
            (_owner != null && other.transform.IsChildOf(_owner.transform))) return;

        if (other.GetComponentInParent<BeanMugwortProjectile>() != null) return;
        if (SaltUseAction.CanHitGhost(other))
        {
            // The same attack ledger survives every ghost: once per target, including child hitboxes.
            if (!_visualOnly && _attack != null && _attack.TryHit(other))
                Debug.Log("[BeanMugwort] 관통 적중 → 피해/퇴치 반응 요청", this);
            return;
        }

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
        if (_visualOnly) ItemVfxPresentation.Impact(transform.position);
        if (_visualOnly && _owner != null)
            _owner.GetComponent<PlayerAudioController>()?.PlayProjectileImpact(transform.position);
        GetComponent<Collider>().enabled = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;

        if (!_visualOnly)
        {
            Debug.Log(reacted ? "[BeanMugwort] 대상 적중 → 피해/퇴치 반응 요청" : $"[BeanMugwort] 투척물 충돌: {other.name}");
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
