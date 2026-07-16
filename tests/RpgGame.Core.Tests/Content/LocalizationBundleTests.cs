using RpgGame.Core.Content.Loading;
using Xunit;

namespace RpgGame.Core.Tests.Content;

public sealed class LocalizationBundleTests
{
    [Fact]
    public void Load_MergesBundlesIndependentlyOfInputOrder()
    {
        LocalizationBundleDocument common = Document(
            "common.json",
            """
            {
              "schemaVersion": 1,
              "locale": "en",
              "texts": {
                "ui.ok": "OK"
              }
            }
            """);
        LocalizationBundleDocument items = Document(
            "items/equipment.json",
            """
            {
              "schemaVersion": 1,
              "locale": "en",
              "texts": {
                "item.sword.name": "Iron Sword"
              }
            }
            """);

        LocalizationBundleLoader loader = new();
        LocalizationLoadResult first = loader.Load("en", [items, common]);
        LocalizationLoadResult second = loader.Load("en", [common, items]);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Catalog!.Texts, second.Catalog!.Texts);
        Assert.Equal("??ui.missing??", first.Catalog.Resolve("ui.missing"));
    }

    [Fact]
    public void Load_RejectsDuplicateKeysAcrossFiles()
    {
        LocalizationLoadResult result = new LocalizationBundleLoader().Load(
            "en",
            [
                Document("a.json", Bundle("ui.ok", "A")),
                Document("b.json", Bundle("ui.ok", "B")),
            ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Problems, problem => problem.Code == "key.duplicate");
    }

    [Fact]
    public void Load_RejectsWrongLocaleAndBlankValues()
    {
        LocalizationLoadResult result = new LocalizationBundleLoader().Load(
            "en",
            [
                Document(
                    "wrong.json",
                    """
                    {
                      "schemaVersion": 1,
                      "locale": "fr",
                      "texts": {
                        "ui.blank": " "
                      }
                    }
                    """),
            ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Problems, problem => problem.Code == "locale.mismatch");
        Assert.Contains(result.Problems, problem => problem.Code == "value.blank");
    }

    private static LocalizationBundleDocument Document(string path, string json) =>
        new(path, json);

    private static string Bundle(string key, string value) =>
        $$"""
        {
          "schemaVersion": 1,
          "locale": "en",
          "texts": {
            "{{key}}": "{{value}}"
          }
        }
        """;
}
