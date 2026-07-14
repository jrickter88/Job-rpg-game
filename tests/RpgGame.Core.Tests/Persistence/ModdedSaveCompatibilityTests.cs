using RpgGame.Core.Mods;
using RpgGame.Core.Persistence;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

/// <summary>Guards the save-to-installed-data-mod compatibility contract.</summary>
public sealed class ModdedSaveCompatibilityTests
{
    private static readonly ModReference ExampleMod = new()
    {
        Id = "mod.example.starter-pack",
        Version = "1.0.0",
    };

    [Fact]
    public async Task SaveAsync_RecordsEnabledModsInStableIdOrder()
    {
        var store = new MemorySaveStore();
        ModReference second = new() { Id = "mod.example.zulu", Version = "2.0.0" };
        ModReference first = new() { Id = "mod.example.alpha", Version = "1.0.0" };
        var coordinator = new SaveCoordinator(
            store,
            "test-build",
            [second, first]);

        await coordinator.SaveAsync("slot_1", CreateState());

        SaveEnvelope saved = store.Save
            ?? throw new InvalidOperationException("No save reached the store.");
        Assert.Equal(
            new[] { "mod.example.alpha", "mod.example.zulu" },
            saved.EnabledMods.Select(mod => mod.Id));
    }

    [Fact]
    public async Task LoadAsync_RejectsMissingRequiredMod()
    {
        var store = new MemorySaveStore
        {
            Save = CreateEnvelope(ExampleMod),
        };
        var coordinator = new SaveCoordinator(store, "test-build");

        MissingSaveModException exception = await Assert.ThrowsAsync<MissingSaveModException>(
            () => coordinator.LoadAsync("slot_1"));

        Assert.Equal(ExampleMod.Id, exception.ModId);
    }

    [Fact]
    public async Task LoadAsync_RejectsDifferentRequiredModVersion()
    {
        var store = new MemorySaveStore
        {
            Save = CreateEnvelope(ExampleMod),
        };
        ModReference installed = new()
        {
            Id = ExampleMod.Id,
            Version = "1.1.0",
        };
        var coordinator = new SaveCoordinator(store, "test-build", [installed]);

        IncompatibleSaveModVersionException exception =
            await Assert.ThrowsAsync<IncompatibleSaveModVersionException>(
                () => coordinator.LoadAsync("slot_1"));

        Assert.Equal("1.0.0", exception.RequiredVersion);
        Assert.Equal("1.1.0", exception.InstalledVersion);
    }

    [Fact]
    public async Task LoadAsync_AllowsPreModSaveWithNoMetadata()
    {
        var store = new MemorySaveStore
        {
            Save = new SaveEnvelope
            {
                State = CreateState(),
            },
        };
        var coordinator = new SaveCoordinator(store, "test-build", [ExampleMod]);

        GameState? loaded = await coordinator.LoadAsync("slot_1");

        Assert.NotNull(loaded);
    }

    private static SaveEnvelope CreateEnvelope(params ModReference[] mods) => new()
    {
        State = CreateState(),
        EnabledMods = mods.ToList(),
    };

    private static GameState CreateState() => new()
    {
        SaveId = "mod-save-test",
    };

    /// <summary>Small port implementation keeps compatibility tests independent of disk IO.</summary>
    private sealed class MemorySaveStore : ISaveStore
    {
        public SaveEnvelope? Save { get; set; }

        public Task<SaveEnvelope?> LoadAsync(
            string slotId,
            CancellationToken cancellationToken = default) => Task.FromResult(Save);

        public Task SaveAsync(
            string slotId,
            SaveEnvelope save,
            CancellationToken cancellationToken = default)
        {
            Save = save;
            return Task.CompletedTask;
        }
    }
}
