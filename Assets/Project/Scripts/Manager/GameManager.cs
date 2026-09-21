using Steamworks;
using System;

public enum GameState
{
    Ongoing,
    Win,
    Lose
};

public class GameManager : SceneSingleton<GameManager>
{
    public int NpcTotal { get; private set; }
    public int NpcCount { get; private set; }
    public int GhostCount { get; private set; }
    public bool IsGameOver { get; private set; }
    public GameState CurrentGameState { get; private set; }

    public event Action OnLose;
    public event Action OnWin;

    public void Init(int totalNpcs, int totalGhosts)
    {
        IsGameOver = false;
        NpcTotal = totalNpcs;
        NpcCount = totalNpcs;
        GhostCount = totalGhosts;
        CurrentGameState = GameState.Ongoing;
    }

    public void SetNpcCount(int count)
    {
        NpcCount = count;

        if ((NpcCount <= NpcTotal / 2) && (CurrentGameState == GameState.Ongoing))
        {
            SetGameState(GameState.Lose);
        }
    }

    public void SetGhostCount(int count)
    {
        GhostCount = count;

        if ((GhostCount <= 0) && (CurrentGameState == GameState.Ongoing))
        {
            SetGameState(GameState.Win);
        }
    }

    public void SetGameState(GameState newState)
    {
        if (CurrentGameState == newState)
        {
            return;
        }
        CurrentGameState = newState;
        OnGameStateChanged();
    }

    public void DecrementNpcCount()
    {
        SetNpcCount(NpcCount - 1);
    }

    public void DecrementGhostCount()
    {
        SetGhostCount(GhostCount - 1);
    }

    private void OnGameStateChanged()
    {
        IsGameOver = (CurrentGameState != GameState.Ongoing);

        switch (CurrentGameState)
        { 
            case (GameState.Win):
                OnWin?.Invoke();
                break;
            case (GameState.Lose):
                OnLose?.Invoke();
                break;
        }
    }


    public void ApplyNetworkState(int npcTotal, int npcCount, int ghostCount, GameState state)
    {
        NpcTotal = npcTotal;
        NpcCount = npcCount;
        GhostCount = ghostCount;

        IsGameOver = state != GameState.Ongoing;

        SetGameState(state);
    }
}
