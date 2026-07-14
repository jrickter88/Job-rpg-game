using RpgGame.Core.Content;
using Xunit;

namespace RpgGame.Core.Tests.Content;

/// <summary>
/// Protects the permanent naming convention used by content references and save data.
/// </summary>
public sealed class ContentIdTests
{
    // A Theory runs the same test body once for every InlineData example. This gives the
    // naming rule a compact executable specification instead of one test per sample.
    [Theory]
    [InlineData("actor.hero.james")]
    [InlineData("ability.black-magic.fire-2")]
    [InlineData("quest.prologue.first-steps")]
    public void IsValid_AcceptsCanonicalIds(string id)
    {
        // Arrange is supplied by InlineData; act and assert fit naturally on one line.
        Assert.True(ContentId.IsValid(id));
    }

    // These cases target common mistakes: missing namespaces, uppercase text, underscores,
    // trailing separators, and empty segments. Adding a newly discovered bad pattern here
    // prevents a later validator refactor from accidentally accepting it.
    [Theory]
    [InlineData("")]
    [InlineData("Fire")]
    [InlineData("ability_fire")]
    [InlineData("ability.fire-")]
    [InlineData("ability..fire")]
    public void IsValid_RejectsUnstableOrAmbiguousIds(string id)
    {
        Assert.False(ContentId.IsValid(id));
    }
}
