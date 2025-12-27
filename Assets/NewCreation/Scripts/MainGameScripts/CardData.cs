using UnityEngine;

public enum CardType { Attack, Defense, Split }

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardData : ScriptableObject
{
    public CardType cardType;
    public string cardName;
    public string description;
    // Add other properties like attack power, defense power, etc. if needed
}
