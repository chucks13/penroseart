using System;

/// <summary>
/// The rotating card deck behind effect and transition casting. Cards are catalog indices; a pulled
/// card rotates to the back so recent picks stay out of the draw window (the front half of the deck).
/// Finds are read-only — a deck rotates only when a card is actually cast, so callers pull at the
/// point the Cue is committed, never while evaluating it.
/// </summary>
public static class Deck
{
    /// <summary>Pulls the card at the requested deck position and rotates it to the back.</summary>
    public static int PullAt(int[] deck, int index)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(nameof(deck));
        }

        if (index < 0 || index >= deck.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var result = deck[index];
        for (var i = index; i < deck.Length - 1; i++)
        {
            deck[i] = deck[i + 1];
        }

        deck[deck.Length - 1] = result;
        return result;
    }

    /// <summary>
    /// Pulls a random eligible card from the draw window, widening to the whole deck when the window
    /// holds no eligible card. Throws when the deck is empty or no card is eligible at all.
    /// </summary>
    public static int PullRandom(int[] deck, Func<int, bool> canSelect, Func<int, int, int> randomRange)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(nameof(deck));
        }

        if (canSelect == null)
        {
            throw new ArgumentNullException(nameof(canSelect));
        }

        if (randomRange == null)
        {
            throw new ArgumentNullException(nameof(randomRange));
        }

        if (deck.Length == 0)
        {
            throw new InvalidOperationException("Cannot pull a card from an empty deck.");
        }

        var drawWindowLength = Math.Max(1, deck.Length / 2);
        var cardIndex = PickEligibleCardIndex(deck, canSelect, drawWindowLength, randomRange);
        if (cardIndex < 0 && drawWindowLength < deck.Length)
        {
            cardIndex = PickEligibleCardIndex(deck, canSelect, deck.Length, randomRange);
        }

        if (cardIndex < 0)
        {
            throw new InvalidOperationException("Cannot pull a card because no deck card is eligible.");
        }

        return PullAt(deck, cardIndex);
    }

    /// <summary>
    /// Finds the first card satisfying the preference without rotating the deck. The caller pulls the
    /// returned position when — and only when — the cast is committed.
    /// </summary>
    public static bool TryFindPreferred(int[] deck, Func<int, bool> isPreferred, out int deckIndex)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(nameof(deck));
        }

        if (isPreferred == null)
        {
            throw new ArgumentNullException(nameof(isPreferred));
        }

        for (var i = 0; i < deck.Length; i++)
        {
            if (isPreferred(deck[i]))
            {
                deckIndex = i;
                return true;
            }
        }

        deckIndex = -1;
        return false;
    }

    private static int PickEligibleCardIndex(
        int[] deck,
        Func<int, bool> canSelect,
        int windowLength,
        Func<int, int, int> randomRange)
    {
        var eligibleCount = 0;
        for (var i = 0; i < windowLength; i++)
        {
            if (canSelect(deck[i]))
            {
                eligibleCount++;
            }
        }

        if (eligibleCount == 0)
        {
            return -1;
        }

        var selectedEligibleOffset = randomRange(0, eligibleCount);
        if (selectedEligibleOffset < 0 || selectedEligibleOffset >= eligibleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(randomRange), "Random range delegate returned a value outside the requested range.");
        }

        for (var i = 0; i < windowLength; i++)
        {
            if (!canSelect(deck[i]))
            {
                continue;
            }

            if (selectedEligibleOffset == 0)
            {
                return i;
            }

            selectedEligibleOffset--;
        }

        return -1;
    }
}
