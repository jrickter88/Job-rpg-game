using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using RpgGame.Core.Mods;
using Xunit;

namespace RpgGame.Core.Tests.Mods;

/// <summary>Executable contract for loose-folder data-mod discovery and ordering.</summary>
public sealed class DirectoryModDiscoveryTests
{
    [Fact]
    public void CheckedInExample_LoadsWithBaseContentAsOneCatalog()
    {
        string modsDirectory = Path.Combine(TestContent.RepositoryRoot, "examples", "mods");
        ModDiscoveryResult modResult = new DirectoryModDiscovery().Discover(modsDirectory);

        Assert.True(modResult.IsSuccess, string.Join(Environment.NewLine, modResult.Problems));
        DiscoveredMod mod = Assert.Single(modResult.Mods);
        Assert.Equal("mod.example.starter-pack", mod.Manifest.Id);

        IContentSource[] sources =
        [
            new DirectoryContentSource(
                ContentSourceIds.Base,
                Path.Combine(TestContent.RepositoryRoot, "game", "content")),
            new DirectoryContentSource(
                mod.Manifest.Id,
                mod.ContentDirectory,
                mod.Manifest.Dependencies),
        ];
        ContentLoadResult contentResult = new JsonContentLoader().Load(sources);

        Assert.True(
            contentResult.IsSuccess,
            string.Join(Environment.NewLine, contentResult.Problems));
        Assert.Equal(17, contentResult.Catalog!.Count);
        Assert.NotNull(contentResult.Catalog.GetRequired<ClassDefinition>(
            "class.example.starter-pack.chronoguard"));
        Assert.NotNull(contentResult.Catalog.GetRequired<AbilityDefinition>(
            "ability.example.starter-pack.temporal-guard"));
    }

    [Fact]
    public void DependencyOrder_PutsRequirementsBeforeDependentsDeterministically()
    {
        using var installation = new TemporaryModInstallation();
        installation.Add("mod.example.addon", ["mod.example.base"]);
        installation.Add("mod.example.base", []);

        ModDiscoveryResult result = new DirectoryModDiscovery().Discover(installation.Root);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal(
            new[] { "mod.example.base", "mod.example.addon" },
            result.Mods.Select(mod => mod.Manifest.Id));
    }

    [Fact]
    public void MissingDependency_ProducesActionableProblemAndEnablesNothing()
    {
        using var installation = new TemporaryModInstallation();
        installation.Add("mod.example.addon", ["mod.example.missing"]);

        ModDiscoveryResult result = new DirectoryModDiscovery().Discover(installation.Root);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Mods);
        Assert.Contains(result.Problems, problem => problem.Code == "dependency.missing");
    }

    [Fact]
    public void DependencyCycle_ProducesActionableProblemAndEnablesNothing()
    {
        using var installation = new TemporaryModInstallation();
        installation.Add("mod.example.first", ["mod.example.second"]);
        installation.Add("mod.example.second", ["mod.example.first"]);

        ModDiscoveryResult result = new DirectoryModDiscovery().Discover(installation.Root);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Mods);
        Assert.Contains(result.Problems, problem => problem.Code == "dependency.cycle");
    }

    /// <summary>
    /// Creates one isolated installation per test. Only manifests and content folders are
    /// required here because content parsing is covered by the checked-in example test.
    /// </summary>
    private sealed class TemporaryModInstallation : IDisposable
    {
        public TemporaryModInstallation()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "RpgGame.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Add(string id, IReadOnlyList<string> dependencies)
        {
            string packageDirectory = Path.Combine(Root, id);
            Directory.CreateDirectory(Path.Combine(packageDirectory, "content"));

            string dependenciesJson = string.Join(
                ", ",
                dependencies.Select(dependency => $"\"{dependency}\""));
            string manifestJson = $$"""
                {
                  "schemaVersion": 1,
                  "id": "{{id}}",
                  "name": "Test {{id}}",
                  "version": "1.0.0",
                  "gameApiVersion": 1,
                  "dependencies": [{{dependenciesJson}}]
                }
                """;
            File.WriteAllText(Path.Combine(packageDirectory, "manifest.json"), manifestJson);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
