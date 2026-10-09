using Match.Api;
using Match.Rules;
using Xunit;

namespace Match.Tests;

public class DeckValidatorTests
{
    static readonly string[] Valid = { "Tank", "Ranger", "Octopus", "Sniper", "SwordMan", "Healer", "Speedster", "Laser", "Controller" };

    [Fact]
    public void AcceptsNineUniquePlayableCharacters()
    {
        List<string> deck = DeckValidator.Validate(Valid);
        Assert.Equal(MatchLimits.DeckSize, deck.Count);
    }

    [Fact]
    public void RejectsDummy()
    {
        var deck = Valid.ToArray();
        deck[0] = CharacterRoster.DummyId;
        Assert.Throws<MatchException>(() => DeckValidator.Validate(deck));
    }

    [Fact]
    public void RejectsDuplicates()
    {
        var deck = Valid.ToArray();
        deck[1] = "Tank";
        Assert.Throws<MatchException>(() => DeckValidator.Validate(deck));
    }

    [Fact]
    public void RejectsUnknownAndShortDecks()
    {
        Assert.Throws<MatchException>(() => DeckValidator.Validate(new[] { "Tank" }));
        var deck = Valid.ToArray();
        deck[2] = "NotAUnit";
        Assert.Throws<MatchException>(() => DeckValidator.Validate(deck));
    }

    [Fact]
    public void RejectsInjectedIds()
    {
        var deck = Valid.ToArray();
        deck[3] = "Tank; DROP";
        Assert.Throws<MatchException>(() => DeckValidator.Validate(deck));
        Assert.Throws<MatchException>(() => InputGuard.MatchId("not-a-guid"));
        Assert.Throws<MatchException>(() => InputGuard.MatchId("../etc/passwd"));
    }
}
