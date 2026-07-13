using System.Text.Json.Nodes;
using RpgGame.Core.Persistence;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

/// <summary>Locks down ordered raw-JSON migration behavior before format 2 is needed.</summary>
public sealed class SaveMigrationRunnerTests
{
    [Fact]
    public void MigrateToCurrent_AppliesAdjacentStepsWithoutMutatingInput()
    {
        var original = new JsonObject
        {
            ["saveFormatVersion"] = 1,
            ["legacyName"] = "Aria",
        };
        var runner = new SaveMigrationRunner(2, [new ExampleVersionOneToTwoMigration()]);

        JsonObject migrated = runner.MigrateToCurrent(original);

        Assert.Equal(1, original["saveFormatVersion"]!.GetValue<int>());
        Assert.True(original.ContainsKey("legacyName"));
        Assert.Equal(2, migrated["saveFormatVersion"]!.GetValue<int>());
        Assert.Equal("Aria", migrated["displayName"]!.GetValue<string>());
        Assert.False(migrated.ContainsKey("legacyName"));
    }

    private sealed class ExampleVersionOneToTwoMigration : ISaveMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public JsonObject Migrate(JsonObject source)
        {
            JsonNode? oldName = source["legacyName"]?.DeepClone();
            source.Remove("legacyName");
            source["displayName"] = oldName;
            source["saveFormatVersion"] = ToVersion;
            return source;
        }
    }
}
