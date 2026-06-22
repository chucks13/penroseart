using System;

/// <summary>
/// Selects the Director's next effect from the rotating effect deck while keeping
/// the currently running effect out of the target slot.
/// </summary>
public static class EffectDeckSelection
{
    /// <summary>
    /// Pulls the next effect card, preferring the requested repertoire when one is available,
    /// and rotates the selected card to the back of the deck.
    /// </summary>
    public static int PullNext(
        int[] deck,
        int currentEffectIndex,
        Repertoire preferredRepertoire,
        Func<int, Repertoire> repertoireForEffect,
        Func<int, int, int> randomRange)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(nameof(deck));
        }

        if (randomRange == null)
        {
            throw new ArgumentNullException(nameof(randomRange));
        }

        if (deck.Length == 0)
        {
            throw new InvalidOperationException("Cannot select a next effect from an empty effect deck.");
        }

        if (preferredRepertoire != Repertoire.None)
        {
            if (repertoireForEffect == null)
            {
                throw new ArgumentNullException(nameof(repertoireForEffect));
            }

            var preferredCardIndex = FindPreferredCardIndex(deck, currentEffectIndex, preferredRepertoire, repertoireForEffect);
            if (preferredCardIndex >= 0)
            {
                return PullCardAt(deck, preferredCardIndex);
            }
        }

        var drawWindowLength = Math.Max(1, deck.Length / 2);
        var cardIndex = PickAllowedCardIndex(deck, currentEffectIndex, drawWindowLength, randomRange);
        if (cardIndex < 0 && drawWindowLength < deck.Length)
        {
            cardIndex = PickAllowedCardIndex(deck, currentEffectIndex, deck.Length, randomRange);
        }

        if (cardIndex < 0)
        {
            throw new InvalidOperationException("Cannot select a next effect because the only available deck card is the current effect.");
        }

        return PullCardAt(deck, cardIndex);
    }

    /// <summary>
    /// Pulls a preferred effect card when one exists, leaving the deck untouched when no suitable
    /// Performer advertises the requested Repertoire.
    /// </summary>
    public static bool TryPullPreferred(
        int[] deck,
        int currentEffectIndex,
        Repertoire preferredRepertoire,
        Func<int, Repertoire> repertoireForEffect,
        out int effectIndex)
    {
        if (deck == null)
        {
            throw new ArgumentNullException(nameof(deck));
        }

        if (preferredRepertoire == Repertoire.None)
        {
            effectIndex = -1;
            return false;
        }

        if (repertoireForEffect == null)
        {
            throw new ArgumentNullException(nameof(repertoireForEffect));
        }

        var preferredCardIndex = FindPreferredCardIndex(deck, currentEffectIndex, preferredRepertoire, repertoireForEffect);
        if (preferredCardIndex < 0)
        {
            effectIndex = -1;
            return false;
        }

        effectIndex = PullCardAt(deck, preferredCardIndex);
        return true;
    }

    private static int FindPreferredCardIndex(
        int[] deck,
        int currentEffectIndex,
        Repertoire preferredRepertoire,
        Func<int, Repertoire> repertoireForEffect)
    {
        for (var i = 0; i < deck.Length; i++)
        {
            var effectIndex = deck[i];
            if (!CanSelect(effectIndex, currentEffectIndex))
            {
                continue;
            }

            if ((repertoireForEffect(effectIndex) & preferredRepertoire) != 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int PickAllowedCardIndex(
        int[] deck,
        int currentEffectIndex,
        int windowLength,
        Func<int, int, int> randomRange)
    {
        var allowedCount = 0;
        for (var i = 0; i < windowLength; i++)
        {
            if (CanSelect(deck[i], currentEffectIndex))
            {
                allowedCount++;
            }
        }

        if (allowedCount == 0)
        {
            return -1;
        }

        var selectedAllowedOffset = randomRange(0, allowedCount);
        if (selectedAllowedOffset < 0 || selectedAllowedOffset >= allowedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(randomRange), "Random range delegate returned a value outside the requested range.");
        }

        for (var i = 0; i < windowLength; i++)
        {
            if (!CanSelect(deck[i], currentEffectIndex))
            {
                continue;
            }

            if (selectedAllowedOffset == 0)
            {
                return i;
            }

            selectedAllowedOffset--;
        }

        return -1;
    }

    private static bool CanSelect(int effectIndex, int currentEffectIndex)
    {
        return currentEffectIndex < 0 || effectIndex != currentEffectIndex;
    }

    private static int PullCardAt(int[] deck, int index)
    {
        var result = deck[index];
        for (var i = index; i < deck.Length - 1; i++)
        {
            deck[i] = deck[i + 1];
        }

        deck[deck.Length - 1] = result;
        return result;
    }
}
