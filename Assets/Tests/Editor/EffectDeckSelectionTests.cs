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
