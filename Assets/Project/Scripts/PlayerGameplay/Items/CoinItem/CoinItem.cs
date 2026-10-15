using UnityEngine;

public class CoinItem : ItemBase
{
    [SerializeField, Min(0.05f)] private float _reuseDelay = 0.5f;
    [SerializeField, Min(0.05f)] private float _feedbackDuration = 0.22f;
    [SerializeField] private Vector3 _feedbackOffset = new Vector3(0f, 0f, 0.018f);
    private float _nextUseTime;
    private Coroutine _feedback;
    private Vector3 _restPosition;
    [Header("Coin")]
    [SerializeField]
    private bool _consumeOnSuccessfulUse;

    public override void OnEquipped()
    {
        if (Data == null)
        {
            return;
        }

        Debug.Log(
            $"[Coin] 장착: {Data.DisplayName}",
            gameObject
        );
    }

    public override void OnUseStarted()
    {
        if (Data == null || !PlayerActionGuard.CanAct(Owner) || Time.time < _nextUseTime) return;
        var controller = Owner.GetComponent<PlayerItemController>();
        if (controller != null && !controller.TryBeginUseCooldown(Data, _reuseDelay)) return;
        _nextUseTime = Time.time + _reuseDelay;
        StopFeedback();
        _restPosition = transform.localPosition;
        _feedback = StartCoroutine(GiveFeedback());
        if (Owner == null)
        {
            return;
        }

        PlayerInteractor interactor =
            Owner.GetComponent<PlayerInteractor>();

        if (interactor == null)
        {
            Debug.LogWarning(
                "[Coin] PlayerInteractor를 찾을 수 없습니다.",
                gameObject
            );

            return;
        }

        if (!interactor.TryGetTargetComponent(
                out ICoinReactable target))
        {
            Debug.Log(
                "[Coin] 엽전을 건넬 대상이 없습니다.",
                gameObject
            );

            return;
        }

        bool reacted =
            target.ReactToCoin(Owner);

        if (!reacted)
        {
            Debug.Log(
                "[Coin] 대상이 엽전에 반응하지 않았습니다.",
                gameObject
            );

            return;
        }

        Debug.Log(
            $"[Coin] 사용 성공: {Data.DisplayName}",
            gameObject
        );

        if (_consumeOnSuccessfulUse)
        {
            ConsumeOne();
        }
    }

    private void ConsumeOne()
    {
        PlayerInventory inventory =
            Owner.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            return;
        }

        inventory.ConsumeItem(
            Data,
            1
        );
    }

    private System.Collections.IEnumerator GiveFeedback()
    {
        float elapsed = 0f;
        while (elapsed < _feedbackDuration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = _restPosition + _feedbackOffset * Mathf.Sin(Mathf.Clamp01(elapsed / _feedbackDuration) * Mathf.PI);
            yield return null;
        }
        transform.localPosition = _restPosition;
        _feedback = null;
    }
    private void StopFeedback()
    {
        if (_feedback == null) return;
        StopCoroutine(_feedback);
        _feedback = null;
        transform.localPosition = _restPosition;
    }
    public override void OnUnequipped() => StopFeedback();
    private void OnDisable() => StopFeedback();
}
