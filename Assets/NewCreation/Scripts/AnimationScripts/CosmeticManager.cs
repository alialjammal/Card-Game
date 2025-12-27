
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Listens to the GameEvents bus on the client-side and triggers cosmetic effects.
/// This script translates abstract game events into actual visual/audio feedback.
/// </summary>
public class CosmeticManager : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // We only want clients to handle cosmetic effects.
        // The server's job is just to run the logic.
        if (!IsClient) return;

        Debug.Log($"[CosmeticManager] Client {OwnerClientId} is subscribing to GameEvents.");
        // Subscribe our handler methods to the events on the bus.
        GameEvents.OnCardAttacked += HandleCardAttackCosmetics;
        GameEvents.OnCardDestroyed += HandleCardDestroyedCosmetics;
        GameEvents.OnCardSplit += HandleCardSplitCosmetics;
    }

    public override void OnNetworkDespawn()
    {
        // ALWAYS unsubscribe from events when this object is destroyed to prevent errors.
        if (!IsClient) return;

        Debug.Log($"[CosmeticManager] Client {OwnerClientId} is unsubscribing from GameEvents.");
        GameEvents.OnCardAttacked -= HandleCardAttackCosmetics;
        GameEvents.OnCardDestroyed -= HandleCardDestroyedCosmetics;
        GameEvents.OnCardSplit -= HandleCardSplitCosmetics;
    }

    private void HandleCardAttackCosmetics(NetworkObject attacker, NetworkObject defender)
    {
        Debug.Log($"[CosmeticManager] EVENT RECEIVED: Card Attack. Attacker: {attacker.name}");
        // Find the cosmetic handler on the card and tell it to play the "Attacked" effect.
        attacker.GetComponent<CardCosmeticHandler>()?.PlayEvent(CardAnimationEvent.Attacked);

        if (defender != null)
        {
            Debug.Log($"[CosmeticManager] EVENT RECEIVED: Card Blocked. Defender: {defender.name}");
            // If there was a defender, tell it to play the "Blocked" effect.
            defender.GetComponent<CardCosmeticHandler>()?.PlayEvent(CardAnimationEvent.Blocked);
        }
    }

    private void HandleCardDestroyedCosmetics(NetworkObject destroyedCard)
    {
        // Check if the object still exists before trying to play an effect on it.
        if (destroyedCard != null)
        {
            Debug.Log($"[CosmeticManager] EVENT RECEIVED: Card Destroyed. Card: {destroyedCard.name}");
            destroyedCard.GetComponent<CardCosmeticHandler>()?.PlayEvent(CardAnimationEvent.WasDestroyed);
        }
    }

    private void HandleCardSplitCosmetics(NetworkObject originalCard, CardType newType)
    {
        if (originalCard != null)
        {
            Debug.Log($"[CosmeticManager] EVENT RECEIVED: Card Split. Card: {originalCard.name}");
            originalCard.GetComponent<CardCosmeticHandler>()?.PlayEvent(CardAnimationEvent.Split);
        }
    }
}
