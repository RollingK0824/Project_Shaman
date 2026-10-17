using UnityEngine;

public interface ICoinReactable
{
    // Called by the authoritative CoinUseAction for a nearby, unobstructed target.
    // Return true only when one coin was accepted; false rejects without spending.
    // Do not remove the coin here: the action invokes the authority's Consume callback once.
    bool ReactToCoin(GameObject user);
}
