using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ArenaLayoutManager : MonoBehaviour
{
    // Consider increasing this value to prevent horizontal cards from overlapping.
    // You might change it from 1.5f to something like 2.0f or 2.5f.
    public float cardSpacing = 2.0f;
    public float layoutAnimationSpeed = 5f;

    public void UpdateLayout()
    {
        Debug.Log($"LAYOUT_MANAGER: UpdateLayout is now running on {gameObject.name}.");

        List<Transform> cards = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                cards.Add(child);
            }
        }

        int cardCount = cards.Count;
        if (cardCount == 0) return;

        float totalWidth = (cardCount - 1) * cardSpacing;
        Vector3 startPosition = transform.position - new Vector3(totalWidth / 2f, 0, 0);

        // Position each card directly.
        for (int i = 0; i < cardCount; i++)
        {
            Vector3 targetPosition = startPosition + new Vector3(i * cardSpacing, 0, 0);

            // --- THE FIX ---
            // Instead of starting a coroutine, just set the position directly.
            // The NetworkTransform on the card will see this change and
            // automatically and smoothly animate it across the network.
            cards[i].position = targetPosition;
        }

    }
}
