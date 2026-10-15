using System;
using System.Collections;
using UnityEngine;

public enum DefenseState { Idle, Countdown, Active }

public class DefenseManager : SceneSingleton<DefenseManager>
{
    [SerializeField]
    private float _startDelay = 20f;

    [SerializeField]
    private MonoBehaviour _targetObject;

    private IDefenseTarget _target;

    public DefenseState State { get; private set; }

    public event Action DefenseStarted;
    public event Action<float> DefenseCountdownStarted;
    public event Action<bool> DefenseCompleted;

    public void StartDefense()
    {
        if (State != DefenseState.Idle)
        {
            Debug.Log("[DefenseManager] A defense is already in progress!");
            return;
        }

        State = DefenseState.Countdown;
        DefenseCountdownStarted?.Invoke(_startDelay);
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        yield return new WaitForSeconds(_startDelay);

        State = DefenseState.Active;
        _target?.ResetHP();
        DefenseStarted?.Invoke();
    }

    private void HandleTargetDestroyed()
    {
        if (State == DefenseState.Active)
        {
            End(false);
        }
        else
        {
            Debug.LogWarning("[DefenseManager] There's no current ongoing Defense.");
        }
    }

    private void HandleDayStart()
    {
        if (State == DefenseState.Active)
        {
            End(true);
        }
        else if (State == DefenseState.Countdown)
        {
            StopAllCoroutines();
            State = DefenseState.Idle;
        }
    }

    private void End(bool won)
    {
        StopAllCoroutines();
        State = DefenseState.Idle;
        DefenseCompleted?.Invoke(won);
    }

    private void OnEnable()
    {
        if (TimeManager.Instance)
        {
            TimeManager.Instance.OnDayStart += HandleDayStart;
        }

        if (_target != null)
        {
            _target.Destroyed += HandleTargetDestroyed;
        }
    }

    private void OnDisable()
    {
        if (TimeManager.Existing)
        {
            TimeManager.Existing.OnDayStart -= HandleDayStart;
        }

        if (_target != null)
        {
            _target.Destroyed -= HandleTargetDestroyed;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _target = _targetObject as IDefenseTarget;
        if (_target == null)
        { 
            Debug.LogError("[DefenseManager] _targetObject doesn't implement IDefenseTarget", this);
        }
    }
}
