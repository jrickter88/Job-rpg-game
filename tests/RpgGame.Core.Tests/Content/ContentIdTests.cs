using RpgGame.Core.Content;
using Xunit;

namespace RpgGame.Core.Tests.Content;

public sealed class ContentIdTests
{
    [Theory]
    [InlineData("actor.hero.aria")]
    [InlineData("ability.black-magic.fire-2")]
    [InlineData("quest.prologue.first-steps")]
    public void IsValid_AcceptsCanonicalIds(string id)
    {
        Assert.True(ContentId.IsValid(id));
    }

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
