using System;
using System.Collections.Generic;
using UnityEngine;

public class ExorcismPrototypeTester : MonoBehaviour
{
    [Header("Prototype Flags")]
    [SerializeField]
    private List<ExorcismFlagInteractable> _flags =
        new List<ExorcismFlagInteractable>();

    [Header("Prototype")]
    [SerializeField]
    private bool _startRitualOnPlay = true;

    public bool IsRitualRunning { get; private set; }

    public int ActivatedFlagCount =>
        GetActivatedFlagCount();

    public int TotalFlagCount =>
        GetValidFlagCount();

    public event Action<int, int> ProgressChanged;
    public event Action RitualCompleted;

    private void OnEnable()
    {
        SubscribeFlags();
    }

    private void Start()
    {
        if (_startRitualOnPlay)
        {
            StartRitual();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFlags();
    }

    public void StartRitual()
    {
        IsRitualRunning = true;

        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag == null)
            {
                continue;
            }

            flag.ResetFlag();
            flag.SetRitualActive(true);
        }

        Debug.Log(
            $"[Exorcism] 의식 시작 / " +
            $"깃발 수 = {TotalFlagCount}"
        );

        ProgressChanged?.Invoke(
            0,
            TotalFlagCount
        );
    }

    public void StopRitual()
    {
        IsRitualRunning = false;

        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag == null)
            {
                continue;
            }

            flag.SetRitualActive(false);
        }

        Debug.Log(
            "[Exorcism] 의식 중단"
        );
    }

    private void HandleFlagActivated(
        ExorcismFlagInteractable flag,
        GameObject activator)
    {
        if (!IsRitualRunning)
        {
            return;
        }

        int activatedCount =
            GetActivatedFlagCount();

        int totalCount =
            GetValidFlagCount();

        Debug.Log(
            $"[Exorcism] 진행도: " +
            $"{activatedCount}/{totalCount}"
        );

        ProgressChanged?.Invoke(
            activatedCount,
            totalCount
        );

        if (totalCount <= 0)
        {
            return;
        }

        if (activatedCount < totalCount)
        {
            return;
        }

        CompleteRitual();
    }

    private void CompleteRitual()
    {
        if (!IsRitualRunning)
        {
            return;
        }

        IsRitualRunning = false;

        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag == null)
            {
                continue;
            }

            flag.SetRitualActive(false);
        }

        Debug.Log(
            "[Exorcism] 모든 깃발 활성화 / " +
            "퇴마 완료 조건 충족"
        );

        RitualCompleted?.Invoke();
    }

    private int GetActivatedFlagCount()
    {
        int count = 0;

        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag != null &&
                flag.IsActivated)
            {
                count++;
            }
        }

        return count;
    }

    private int GetValidFlagCount()
    {
        int count = 0;

        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag != null)
            {
                count++;
            }
        }

        return count;
    }

    private void SubscribeFlags()
    {
        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag == null)
            {
                continue;
            }

            flag.Activated -=
                HandleFlagActivated;

            flag.Activated +=
                HandleFlagActivated;
        }
    }

    private void UnsubscribeFlags()
    {
        foreach (ExorcismFlagInteractable flag in _flags)
        {
            if (flag == null)
            {
                continue;
            }

            flag.Activated -=
                HandleFlagActivated;
        }
    }
}