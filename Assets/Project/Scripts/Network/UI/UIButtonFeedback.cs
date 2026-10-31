using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메뉴 버튼 공용 피드백: 호버 시 살짝 커지고 소리, 클릭 시 소리
// 버튼 오브젝트에 붙이면 됨 (여러 개 선택 후 한 번에 붙이고 클립 지정 가능)
[RequireComponent(typeof(Selectable))]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("호버")]
    [SerializeField] private float _hoverScale = 1.05f;
    [SerializeField] private float _duration = 0.12f;

    [Header("사운드 (비우면 재생 안 함)")]
    [SerializeField] private AudioClip _hoverSound;
    [SerializeField] private AudioClip _clickSound;

    private Selectable _selectable;
    private Vector3 _baseScale;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;

        // 실제로 눌렸을 때만 소리 (비활성 버튼 클릭은 무시됨)
        if (_selectable is Button button) button.onClick.AddListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_selectable.IsInteractable()) return;

        ScaleTo(_baseScale * _hoverScale);
        PlayUI(_hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ScaleTo(_baseScale);
    }

    // 호버 중에 패널이 닫혀도 다음에 열릴 때 커진 채로 남지 않게
    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = _baseScale;
    }

    private void ScaleTo(Vector3 target)
    {
        transform.DOKill();
        // 일시정지(timeScale 0) 중에도 동작하도록 unscaled
        transform.DOScale(target, _duration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void PlayClick() => PlayUI(_clickSound);

    private static void PlayUI(AudioClip clip)
    {
        if (clip != null) SoundManager.Instance.PlayUI(clip);
    }
}
