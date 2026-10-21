using UnityEngine;
using System;

// 아이템 경직, 둔화, 감각 방해를 받는 공용 수신사
// 효과 수락, 중첩, 만료만 관리
// 실제 행동 변화는 대상 AI가 아래 상태를 읽어서 적용
// HP는 다루지 않음(IDamageable 담당), 호출은 서버.오프라인에서만 들어옴(ItemAttackAuthority가 보장)
[DisallowMultipleComponent]
public class ItemControlReceiver : MonoBehaviour, IItemControlReceiver, ISaltReactable
{
    [Header("받는 효과")]
    [SerializeField] private bool _acceptSlow = true;
    [SerializeField] private bool _acceptStagger = true;
    [SerializeField] private bool _acceptSensory = true;

    [Header("소금 반응 (HP 없는 대상용, 0이면 반응 없음)")]
    [SerializeField, Min(0f)] private float _saltStaggerDuration;

    private float _slowUntil;
    private float _slowMultiplier = 1f;
    private float _staggerUntil;
    private float _visionBlockedUntil;
    private float _hearingBlockedUntil;

    // AI가 읽는 상태. 시간 기준이라 만료되면 자동으로 원래 값
    public float SpeedMultiplier => Time.time < _slowUntil ? _slowMultiplier : 1f;
    public bool IsStaggered => Time.time < _staggerUntil;
    public bool IsVisionBlocked => Time.time < _visionBlockedUntil;
    public bool IsHearingBlocked => Time.time < _hearingBlockedUntil;

    // 효과가 들어온 순간 (연출·로그·나중에 클라 표시용)
    public event Action<ItemControlEffect, GameObject> Applied;

    public bool TryApplyItemControl(ItemControlEffect effect, GameObject source)
    {
        if (!effect.IsValid || !isActiveAndEnabled) return false;

        float until = Time.time + effect.Duration;

        switch(effect.Kind)
        {
            case ItemControlKind.Slow:
                if (!_acceptSlow) return false;
                // 겹치면 더 강한 둔화, 더 늦은 만료
                _slowMultiplier = Time.time < _slowUntil ? Mathf.Min(_slowMultiplier, effect.SpeedMultiplier) : effect.SpeedMultiplier;
                _slowUntil = Mathf.Max(_slowUntil, until);
                break;

            case ItemControlKind.Stagger:
                if (!_acceptStagger) return false;
                _staggerUntil = Mathf.Max(_staggerUntil, until);
                break;

            case ItemControlKind.SensoryInterference:
                if (!_acceptSensory) return false;
                if ((effect.Senses & ItemSense.Vision) != 0) _visionBlockedUntil = Mathf.Max(_visionBlockedUntil, until);
                if ((effect.Senses & ItemSense.Hearing) != 0) _hearingBlockedUntil = Mathf.Max(_hearingBlockedUntil, until);
                break;

            default:
                return false;
        }

        Applied?.Invoke(effect, source);
        return true;
    }

    // 소금: HP가 있는 대상(잡귀)은 IDamageable 피해가 우선이라 여기까지 오지 않음. 원귀처럼 HP가 없을 때만 경직
    public bool ReactToSalt(GameObject user)
    {
        if (_saltStaggerDuration <= 0f) return false;
        return TryApplyItemControl(new ItemControlEffect { Kind = ItemControlKind.Stagger, Duration = _saltStaggerDuration }, user);
    }
}
