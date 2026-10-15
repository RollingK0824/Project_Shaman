using System;

public interface IDefenseTarget
{
    float CurrentHP { get; }
    float MaxHP { get; }
    event Action Destroyed;
    void ResetHP();
}
