using UnityEngine;
using Unity.Netcode; // <-- Add this namespace
using System.Collections.Generic;

// --- MODIFIED: Inherit from NetworkBehaviour ---
public class Player : NetworkBehaviour
{
    // --- MODIFIED: Use NetworkVariable for health ---
    // This automatically syncs the health value from the server to all clients.
    // The server has write permission, clients have read permission.
    public NetworkVariable<int> health = new NetworkVariable<int>(5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // This list is now managed on the server. Clients won't directly modify it.
    public List<CardData> arenaCards = new List<CardData>();

    // --- OVERRIDE: OnNetworkSpawn ---
    // This is like Start(), but it's called when the object is spawned on the network.
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Value = 5; // Initialize health on the server
        }
    }

    public bool HasDefenseCard()
    {
        return arenaCards.Exists(card => card.cardType == CardType.Defense);
    }

    // TakeDamage will now be a server-only action.
    // We don't need to change the code, but we must remember to only call it on the server.
    public void TakeDamage(int amount)
    {
        if (!IsServer) return; // Only the server can modify health

        health.Value -= amount;
        Debug.Log("Player " + OwnerClientId + " takes " + amount + " damage. Health is now: " + health.Value);
        if (health.Value <= 0)
        {
            health.Value = 0;
            Debug.Log("Player " + OwnerClientId + " has been defeated!");
        }
    }
}
