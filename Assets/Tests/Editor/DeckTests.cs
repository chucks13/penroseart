using System;
using NUnit.Framework;

public sealed class DeckTests
{
    [Test]
    public void PullRandomSkipsIneligibleCardInDrawWindow()
    {
        var deck = new[] { 2, 1, 0, 3 };

        var selected = Deck.PullRandom(
            deck,
            canSelect: cardIndex => cardIndex != 2,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(selected, Is.EqualTo(1));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 3, 1 }));
    }

    [Test]
    public void PullRandomWidensToWholeDeckWhenDrawWindowHasNoEligibleCard()
    {
        var deck = new[] { 2, 1, 0, 3 };

        var selected = Deck.PullRandom(
            deck,
            canSelect: cardIndex => cardIndex is 0 or 3,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(selected, Is.EqualTo(0));
        Assert.That(deck, Is.EqualTo(new[] { 2, 1, 3, 0 }));
    }

    [Test]
    public void PullRandomFailsPlainlyWhenNoCardIsEligible()
    {
        var deck = new[] { 2 };

        Assert.Throws<InvalidOperationException>(() => Deck.PullRandom(
            deck,
            canSelect: cardIndex => cardIndex != 2,
            randomRange: (minInclusive, _) => minInclusive));
    }

    [Test]
    public void TryFindPreferredReturnsDeckPositionWithoutRotatingDeck()
    {
        var deck = new[] { 1, 2, 0 };

        var found = Deck.TryFindPreferred(deck, cardIndex => cardIndex == 2, out var deckIndex);

        Assert.That(found, Is.True);
        Assert.That(deckIndex, Is.EqualTo(1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void TryFindPreferredLeavesDeckWhenNoCardMatches()
    {
        var deck = new[] { 1, 2, 0 };

        var found = Deck.TryFindPreferred(deck, _ => false, out var deckIndex);

        Assert.That(found, Is.False);
        Assert.That(deckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void PullAtRotatesSelectedCardToTheBack()
    {
        var deck = new[] { 1, 2, 0 };

        var selected = Deck.PullAt(deck, 1);

        Assert.That(selected, Is.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 1, 0, 2 }));
    }
}
