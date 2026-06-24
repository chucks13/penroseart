using System;
using NUnit.Framework;

public sealed class EffectDeckSelectionTests
{
    [Test]
    public void PullNextSkipsCurrentEffectWhenItIsInDrawWindow()
    {
        var deck = new[] { 2, 1, 0, 3 };

        var selected = EffectDeckSelection.PullNext(
            deck,
            currentEffectIndex: 2,
            preferredRepertoire: Repertoire.None,
            repertoireForEffect: _ => Repertoire.None,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(selected, Is.EqualTo(1));
        Assert.That(selected, Is.Not.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 3, 1 }));
    }

    [Test]
    public void PullNextSkipsCurrentEffectWhenItMatchesPreferredRepertoire()
    {
        var deck = new[] { 2, 1, 0, 3 };

        var selected = EffectDeckSelection.PullNext(
            deck,
            currentEffectIndex: 2,
            preferredRepertoire: Repertoire.HandlesDrop,
            repertoireForEffect: effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(selected, Is.EqualTo(1));
        Assert.That(selected, Is.Not.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 3, 1 }));
    }

    [Test]
    public void TryPullPreferredPullsMatchingPerformerAndRotatesDeck()
    {
        var deck = new[] { 1, 2, 0 };

        var found = EffectDeckSelection.TryPullPreferred(
            deck,
            currentEffectIndex: 0,
            preferredRepertoire: Repertoire.HandlesDrop,
            repertoireForEffect: effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            out var selected);

        Assert.That(found, Is.True);
        Assert.That(selected, Is.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 1, 0, 2 }));
    }

    [Test]
    public void TryPullPreferredSkipsCurrentEffectWhenItMatchesPreferredRepertoire()
    {
        var deck = new[] { 2, 1, 0 };

        var found = EffectDeckSelection.TryPullPreferred(
            deck,
            currentEffectIndex: 2,
            preferredRepertoire: Repertoire.HandlesDrop,
            repertoireForEffect: effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None,
            out var selected);

        Assert.That(found, Is.True);
        Assert.That(selected, Is.EqualTo(1));
        Assert.That(selected, Is.Not.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 1 }));
    }

    [Test]
    public void TryPullPreferredLeavesDeckWhenNoMatchingPerformerExists()
    {
        var deck = new[] { 1, 2, 0 };

        var found = EffectDeckSelection.TryPullPreferred(
            deck,
            currentEffectIndex: 0,
            preferredRepertoire: Repertoire.HandlesDrop,
            repertoireForEffect: _ => Repertoire.None,
            out var selected);

        Assert.That(found, Is.False);
        Assert.That(selected, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void PullNextFailsPlainlyWhenOnlyTheCurrentEffectIsAvailable()
    {
        var deck = new[] { 2 };

        Assert.Throws<InvalidOperationException>(() => EffectDeckSelection.PullNext(
            deck,
            currentEffectIndex: 2,
            preferredRepertoire: Repertoire.None,
            repertoireForEffect: _ => Repertoire.None,
            randomRange: (minInclusive, _) => minInclusive));
    }
}

public sealed class TransitionDeckSelectionTests
{
    [Test]
    public void TryFindPreferredReturnsMatchingTransitionWithoutRotatingDeck()
    {
        var deck = new[] { 1, 2, 0 };

        var found = TransitionDeckSelection.TryFindPreferred(
            deck,
            Repertoire.HandlesDrop,
            transitionIndex => transitionIndex == 2 ? RepertoireFor(Repertoire.HandlesDrop) : RepertoireFor(Repertoire.None),
            _ => true,
            out var deckIndex,
            out var selected);

        Assert.That(found, Is.True);
        Assert.That(deckIndex, Is.EqualTo(1));
        Assert.That(selected, Is.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void PullAtRotatesSelectedTransitionCard()
    {
        var deck = new[] { 1, 2, 0 };

        var selected = TransitionDeckSelection.PullAt(deck, 1);

        Assert.That(selected, Is.EqualTo(2));
        Assert.That(deck, Is.EqualTo(new[] { 1, 0, 2 }));
    }

    private static TransitionRepertoire RepertoireFor(Repertoire tags)
    {
        return TransitionRepertoire.FromRunwayAndTail(
            tags,
            runwayBeats: 4,
            tailBeats: 0,
            TransitionShape.Blend,
            TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
    }
}
