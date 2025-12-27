
using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A static event bus for broadcasting high-level game events.
/// The GameManager (server) invokes events.
/// Managers like CosmeticManager and SoundManager (clients) subscribe to them.
/// </summary>
public static class GameEvents
{
    // -- Define the "Actions" that represent our game events --

    // Called when an attack action occurs.
    // Passes the attacker and the defender (which can be null for a direct hit).
    public static event Action<NetworkObject, NetworkObject> OnCardAttacked;

    // Called right before a card is destroyed and despawned.
    public static event Action<NetworkObject> OnCardDestroyed;

    // Called when a 'V' card is split.
    // Passes the original card and the type of the two new cards.
    public static event Action<NetworkObject, CardType> OnCardSplit;


    // -- Define the "Invoke" methods that the server will call --

    public static void InvokeCardAttacked(NetworkObject attacker, NetworkObject defender)
    {
        Debug.Log($"[EVENT-BUS] InvokeCardAttacked called. Is anyone listening? (OnCardAttacked == null): {OnCardAttacked == null}");
        // The '?' ensures we only call this if at least one script has subscribed to the event.
        OnCardAttacked?.Invoke(attacker, defender);
    }

    public static void InvokeCardDestroyed(NetworkObject destroyedCard)
    {
        OnCardDestroyed?.Invoke(destroyedCard);
    }

    public static void InvokeCardSplit(NetworkObject originalCard, CardType newType)
    {
        OnCardSplit?.Invoke(originalCard, newType);
    }
}
