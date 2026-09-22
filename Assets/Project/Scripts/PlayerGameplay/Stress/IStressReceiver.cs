using UnityEngine;

public interface IStressReceiver
{
    void ReceiveStress(
        float amount,
        StressCause cause,
        GameObject source = null
    );
}