using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Events;

public enum GameState
{
    Ongoing,
    Win,
    Lose
};

public class GameManager : SceneSingleton<GameManager>
{
    public int CurrentNpcTotal { get; private set; }
    public bool isGameover { get; private set; }
    public UnityAction OnLose;
    public UnityAction OnWin;

    private GameState _currentGameState = GameState.Ongoing;
    [SerializeField] private int NpcTotal;
    private int _ghostTotal;

    public void Start()
    {
        isGameover = false;
        CurrentNpcTotal = NpcTotal;
    }

    public void ReportGhostTotal(int total)
    {
        _ghostTotal = total;
    }

    public void UpdateNpcCount()
    {
        if (_currentGameState == GameState.Ongoing)
        {
            --CurrentNpcTotal;
            if (CurrentNpcTotal == NpcTotal / 2)
            {
                _currentGameState = GameState.Lose;
                OnGameStateChanged();
            }
        }
    }

    public void UpdateGhostCount()
    {
        if (_currentGameState == GameState.Ongoing)
        {
            --_ghostTotal;
            if (_ghostTotal == 0)
            {
                _currentGameState = GameState.Win;
                OnGameStateChanged();
            }
        }
    }

    private void OnGameStateChanged()
    {
        switch (_currentGameState)
        { 
            case (GameState.Win):
                OnWin.Invoke();
                break;
            case (GameState.Lose):
                OnLose.Invoke();
                break;
        }

        isGameover = true;
    }
}
