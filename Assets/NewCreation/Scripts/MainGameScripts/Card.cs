using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class Card : NetworkBehaviour
{
    public NetworkVariable<int> cardDataId = new NetworkVariable<int>();
    public CardData cardData { get; private set; }
    private GameManager gameManager;
    public Text cardText;

    public override void OnNetworkSpawn()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        cardDataId.OnValueChanged += OnCardDataIdChanged;
        OnCardDataIdChanged(0, cardDataId.Value); // Run once for initial state
    }

    private void OnCardDataIdChanged(int previousValue, int newValue)
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        cardData = gameManager.GetCardDataFromId(newValue);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (cardData != null)
        {
            if (cardText != null) cardText.text = cardData.cardName;
            transform.rotation = (cardData.cardType == CardType.Defense) ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
        }
    }

    private void OnMouseDown()
    {
        if (gameManager != null && IsOwner)
        {
            gameManager.SelectCard(this);
        }
    }
}
