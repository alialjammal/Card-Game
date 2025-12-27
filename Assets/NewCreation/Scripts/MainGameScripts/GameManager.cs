using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP; // To get the transport component

public class GameManager : NetworkBehaviour
{
    [Header("Prefab References")]
    public GameObject playerPrefab; // The PlayerPrefab with NetworkObject
    public GameObject cardPrefab;   // The Card prefab with NetworkObject

    [Header("Card Assets")]
    public CardData attackCardAsset;
    public CardData defenseCardAsset;
    public CardData splitCardAsset;

    [Header("Scene References")]
    public ArenaLayoutManager player1LayoutManager;
    public ArenaLayoutManager player2LayoutManager;

    [Header("UI References")]
    public GameObject multiplayerControlPanel;
    public GameObject actionPanel;
    public Button attackButton;
    public Button splitIntoA_Button;
    public Button splitIntoD_Button;
    public Button splitIntoV_Button;
    public Text turnText; // A UI text element to show whose turn it is

    // --- NETWORK VARIABLES ---
    // These variables are automatically synchronized from the server to all clients.
    private NetworkVariable<int> turnIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkList<ulong> connectedPlayerIds; // List of connected client IDs

    // --- SERVER-SIDE STATE ---
    // These lists help the server keep track of players and their cards.
    private List<Player> players = new List<Player>();
    private Dictionary<ulong, List<Card>> playerCards = new Dictionary<ulong, List<Card>>();


    [Header("New Relay UI")]
    public Button createGameButton;
    public Button joinGameButton;
    public InputField joinCodeInput; // Use InputField or TMP_InputField
    public Text joinCodeText; // A text element to SHOW the host their join code

    // --- CLIENT-SIDE STATE ---
    private Card selectedCard;

    // --- CARD MAPPING ---
    // Used to identify CardData assets across the network using a simple ID.
    private Dictionary<int, CardData> cardIdMap = new Dictionary<int, CardData>();

    #region Setup & Initialization

    private void Awake()
    {
        // Initialize the network list
        connectedPlayerIds = new NetworkList<ulong>();

        // Create the mapping from asset instance ID to the asset itself.
        // This is consistent across server and clients.
        cardIdMap.Add(attackCardAsset.GetInstanceID(), attackCardAsset);
        cardIdMap.Add(defenseCardAsset.GetInstanceID(), defenseCardAsset);
        cardIdMap.Add(splitCardAsset.GetInstanceID(), splitCardAsset);
    }
    private async void Start()
    {
        // Initialize Unity Services
        await UnityServices.InitializeAsync();

        // Sign in the player anonymously
        AuthenticationService.Instance.SignedIn += () => {
            Debug.Log("Signed in as " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // --- NEW: Hook up the new UI buttons ---
        createGameButton.onClick.AddListener(CreateRelayGame);
        joinGameButton.onClick.AddListener(JoinRelayGame);
    }
    // --- UPDATED FUNCTION for the Host ---
    private async void CreateRelayGame()
    {
        try
        {
            // 1. Create a relay allocation (this part is the same)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1); // 1 = max players minus host

            // 2. Get the join code (this part is the same)
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + joinCode);
            joinCodeText.text = "Join Code: " + joinCode;

            // --- 3. THE MODERN WAY to configure the transport for the HOST ---
            // This replaces the old `transport.SetRelayServerData(...)`
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // 4. Start the host (this part is the same)
            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay creation failed: " + e.Message);
        }
    }

    // --- UPDATED FUNCTION for the Client ---
    private async void JoinRelayGame()
    {
        try
        {
            string joinCode = joinCodeInput.text;
            Debug.Log("Attempting to join Relay with code: " + joinCode);

            // 1. Join the relay allocation (this part is the same)
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // --- 2. THE MODERN WAY to configure the transport for the CLIENT ---
            // This also replaces the old `transport.SetRelayServerData(...)`
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            // 3. Start the client (this part is the same)
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay join failed: " + e.Message);
        }
    }


    public override void OnNetworkSpawn()
    {
        // Subscribe to events that happen on both server and client
        turnIndex.OnValueChanged += OnTurnChanged;
        UpdateTurnUI(0, turnIndex.Value); // Update UI on join
        Debug.Log($"[COSMETIC-MGR] OnNetworkSpawn has been called on Client {OwnerClientId}. IsClient: {IsClient}");
        if (IsServer)
        {
            // Subscribe to server-only events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

            // Add the host as the first player
            OnClientConnected(NetworkManager.Singleton.LocalClientId);
        }

        // Add listeners to the UI buttons. This happens on every client.
        attackButton.onClick.AddListener(OnAttackButton);
        splitIntoA_Button.onClick.AddListener(() => OnSplitButton(CardType.Attack));
        splitIntoD_Button.onClick.AddListener(() => OnSplitButton(CardType.Defense));
        splitIntoV_Button.onClick.AddListener(() => OnSplitButton(CardType.Split));

        HideActionPanel();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (connectedPlayerIds.Contains(clientId)) return;
        if (connectedPlayerIds.Count >= 2) return;

        Debug.Log($"Client {clientId} connected.");
        connectedPlayerIds.Add(clientId);
        playerCards.Add(clientId, new List<Card>());

        NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerNetworkObject != null)
        {
            // --- FIX ---
            // Get the component and assign it to the variable.
            // Do NOT write "Player newPlayer = ..." here.
            Player newPlayer = playerNetworkObject.GetComponent<Player>();

            players.Add(newPlayer);
            Debug.Log($"Found and added player for client {clientId}.");
        }
        else
        {
            Debug.LogError($"Could not find player object for client {clientId}.");
        }

        if (connectedPlayerIds.Count == 2)
        {
            StartGame();
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"Client {clientId} disconnected.");
        // Handle player disconnection logic (e.g., end game)
    }

    private void StartGame()
    {
        if (!IsServer) return;
        Debug.Log("Two players connected. Starting game.");

        HideMultiplayerControlPanelClientRpc();
        // Spawn initial cards for each player
        foreach (var clientId in connectedPlayerIds)
        {
            SpawnCardForPlayer(clientId, attackCardAsset);
            SpawnCardForPlayer(clientId, defenseCardAsset);
            SpawnCardForPlayer(clientId, splitCardAsset);
        }
    }

    #endregion

    #region Card & Player Actions (Client-Side)

    // Called by Card.cs when a card is clicked
    public void SelectCard(Card card)
    {
        // Check if it's our turn and we own the card
        if (turnIndex.Value != (int)NetworkManager.Singleton.LocalClientId || card.OwnerClientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Cannot select card: Not your turn or not your card.");
            return;
        }

        selectedCard = card;

        // Request the server to show the appropriate action panel
        ShowActionPanelServerRpc(card.NetworkObjectId);
    }

    // Called when a UI button is clicked
    private void OnAttackButton()
    {
        if (selectedCard == null) return;
        UseAttackCardServerRpc(selectedCard.NetworkObjectId);
        HideActionPanel();
    }

    private void OnSplitButton(CardType choice)
    {
        if (selectedCard == null) return;
        UseSplitCardServerRpc(selectedCard.NetworkObjectId, choice);
        HideActionPanel();
    }

    #endregion

    #region Server RPCs (Client -> Server)

    [ServerRpc(RequireOwnership = false)]
    private void ShowActionPanelServerRpc(ulong cardId, ServerRpcParams rpcParams = default)
    {
        NetworkObject cardObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[cardId];
        Card card = cardObj.GetComponent<Card>();

        // Determine which UI to show
        bool showAttack = card.cardData.cardType == CardType.Attack;
        bool showSplit = card.cardData.cardType == CardType.Split;

        // Send a message back to the specific client who requested this
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } }
        };
        ShowActionPanelClientRpc(showAttack, showSplit, clientRpcParams);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UseAttackCardServerRpc(ulong attackerCardId)
    {
        if (!IsServer) return;

        NetworkObject attackerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerCardId];
        Card attackerCard = attackerObj.GetComponent<Card>();
        ulong attackerId = attackerCard.OwnerClientId;
        ulong opponentId = GetOpponentId(attackerId);

        Player opponentPlayer = GetPlayerForId(opponentId);
        Card defenseCard = FindCardObject(opponentId, CardType.Defense);

        if (defenseCard != null)
        {
            Debug.Log($"[GameManager] Attack Blocked. Announcing event.");
            // Announce the game event. No more ClientRpc!
            GameEvents.InvokeCardAttacked(attackerCard.NetworkObject, defenseCard.NetworkObject);
            DespawnCard(defenseCard);
        }
        else
        {
            Debug.Log($"[GameManager] Direct Hit. Announcing event.");
            // Announce the game event with a null defender.
            GameEvents.InvokeCardAttacked(attackerCard.NetworkObject, null);
            opponentPlayer.TakeDamage(1);
        }

        DespawnCard(attackerCard);
        SwitchTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    private void UseSplitCardServerRpc(ulong cardId, CardType choice)
    {
        if (!IsServer) return;

        NetworkObject cardObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[cardId];
        Card card = cardObj.GetComponent<Card>();
        ulong ownerId = card.OwnerClientId;

        // Announce the split event BEFORE despawning the original card.
        GameEvents.InvokeCardSplit(card.NetworkObject, choice);

        DespawnCard(card);

        CardData newCardData = GetCardDataFromType(choice);
        SpawnCardForPlayer(ownerId, newCardData);
        SpawnCardForPlayer(ownerId, newCardData);

        SwitchTurn();
    }
    // Add these two helper methods to your GameManager.cs script

    private ulong GetOpponentId(ulong attackerId)
    {
        // Loop through the list of connected players.
        // Return the ID that is NOT the attacker's ID.
        foreach (ulong clientId in connectedPlayerIds)
        {
            if (clientId != attackerId)
            {
                return clientId;
            }
        }
        // Return a default/invalid value if no opponent is found.
        return 0;
    }

    private Player GetPlayerForId(ulong clientId)
    {
        // Use LINQ's FirstOrDefault to find the player component
        // associated with the given client ID.
        foreach (var player in players)
        {
            if (player.OwnerClientId == clientId)
            {
                return player;
            }
        }
        return null; // Return null if no player is found
    }


    #endregion

    #region Client RPCs (Server -> Client)

    [ClientRpc]
    private void ShowActionPanelClientRpc(bool showAttack, bool showSplit, ClientRpcParams clientRpcParams = default)
    {
        actionPanel.SetActive(true);
        attackButton.gameObject.SetActive(showAttack);
        splitIntoA_Button.gameObject.SetActive(showSplit);
        splitIntoD_Button.gameObject.SetActive(showSplit);
        splitIntoV_Button.gameObject.SetActive(showSplit);
    }

    [ClientRpc]
    private void HideActionPanelClientRpc()
    {
        HideActionPanel();
    }

    [ClientRpc]
    private void HideMultiplayerControlPanelClientRpc()
    {
        if (multiplayerControlPanel != null)
        {
            multiplayerControlPanel.SetActive(false);
        }
    }

    #endregion

    #region Server-Side Logic & Helpers

    private void SwitchTurn()
    {
        if (!IsServer) return;
        turnIndex.Value = (turnIndex.Value + 1) % 2;
    }


    // In GameManager.cs

    private void SpawnCardForPlayer(ulong ownerId, CardData data)
    {
        if (!IsServer) return;

        ArenaLayoutManager layoutManager = GetLayoutManager(ownerId);
        if (layoutManager == null)
        {
            Debug.LogError($"Cannot find layout manager for owner {ownerId}.");
            return;
        }

        // 1. Instantiate the card.
        GameObject cardObj = Instantiate(cardPrefab);

        // 2. Get components.
        NetworkObject netObj = cardObj.GetComponent<NetworkObject>();
        Card cardScript = cardObj.GetComponent<Card>();

        // 3. Spawn the object over the network.
        netObj.Spawn(true);

        // 4. Assign ownership and data FIRST.
        // This gives the object a moment to exist on the network before we change its hierarchy.
        netObj.ChangeOwnership(ownerId);
        cardScript.cardDataId.Value = data.GetInstanceID();

        // 5. Add to server-side list for tracking.
        playerCards[ownerId].Add(cardScript);

        // 6. Set the parent as the LAST step in the spawn process.
        // This ensures the parent object (the arena) is already known to the clients.
        netObj.transform.SetParent(layoutManager.transform, true);

        // 7. Run layout logic ON THE SERVER.
        layoutManager.UpdateLayout();
    }


    private void DespawnCard(Card card)
    {
        if (!IsServer || card == null) return;

        ulong ownerId = card.OwnerClientId;

        // Announce the destruction event.
        GameEvents.InvokeCardDestroyed(card.NetworkObject);

        if (playerCards.ContainsKey(ownerId) && playerCards[ownerId].Contains(card))
        {
            playerCards[ownerId].Remove(card);
        }

        card.GetComponent<NetworkObject>().Despawn(true);
    }

    private Card FindCardObject(ulong ownerId, CardType type)
    {
        return playerCards[ownerId].FirstOrDefault(c => c.cardData.cardType == type);
    }

    #endregion

    #region UI & Client-Side Helpers

    private void OnTurnChanged(int previousValue, int newValue)
    {
        UpdateTurnUI(previousValue, newValue);
    }

    private void UpdateTurnUI(int previousValue, int newValue)
    {
        if (turnText == null) return;

        if (connectedPlayerIds.Count < 2)
        {
            turnText.text = "Waiting for players...";
            return;
        }

        ulong currentTurnClientId = connectedPlayerIds[newValue];
        if (currentTurnClientId == NetworkManager.Singleton.LocalClientId)
        {
            turnText.text = "Your Turn";
        }
        else
        {
            turnText.text = $"Player {currentTurnClientId}'s Turn";
        }
    }

    private void HideActionPanel()
    {
        if (actionPanel != null) actionPanel.SetActive(false);
        selectedCard = null;
    }

    public CardData GetCardDataFromId(int id)
    {
        cardIdMap.TryGetValue(id, out CardData data);
        return data;
    }

    private CardData GetCardDataFromType(CardType type)
    {
        switch (type)
        {
            case CardType.Attack: return attackCardAsset;
            case CardType.Defense: return defenseCardAsset;
            case CardType.Split: return splitCardAsset;
            default: return null;
        }
    }

    private ArenaLayoutManager GetLayoutManager(ulong ownerId)
    {
        int playerIndex = -1; // Default to an invalid index
        for (int i = 0; i < connectedPlayerIds.Count; i++)
        {
            if (connectedPlayerIds[i] == ownerId)
            {
                playerIndex = i;
                break; // Exit the loop once we've found the index
            }
        }
        return playerIndex == 0 ? player1LayoutManager : player2LayoutManager;
    }

    #endregion
}
