using UnityEngine;

public class CoinItem : ItemBase
{
    [SerializeField, Min(0.05f)] private float _feedbackDuration = 0.22f;
    [SerializeField] private Vector3 _feedbackOffset = new Vector3(0f, 0f, 0.018f);
    private Coroutine _feedback;
    private Vector3 _restPosition;

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
        if (!TryBuildUseRequest(out var context)) return;
        StopFeedback();
        _restPosition = transform.localPosition;
        _feedback = StartCoroutine(GiveFeedback());
        PlayerEvents.RaiseItemUseRequested(context);
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
