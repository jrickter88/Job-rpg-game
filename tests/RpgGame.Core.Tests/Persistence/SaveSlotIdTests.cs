using RpgGame.Core.Persistence;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

/// <summary>Protects the filesystem boundary from traversal and nonportable slot names.</summary>
public sealed class SaveSlotIdTests
{
    [Theory]
    [InlineData("slot_1")]
    [InlineData("autosave-2")]
    public void RequireValid_AcceptsPortableNames(string slotId)
    {
        Assert.Equal(slotId, SaveSlotId.RequireValid(slotId));
    }

    [Theory]
    [InlineData("../slot")]
    [InlineData("folder/slot")]
    [InlineData("slot with spaces")]
    public void RequireValid_RejectsUnsafeNames(string slotId)
    {
        Assert.Throws<ArgumentException>(() => SaveSlotId.RequireValid(slotId));
    }
}
