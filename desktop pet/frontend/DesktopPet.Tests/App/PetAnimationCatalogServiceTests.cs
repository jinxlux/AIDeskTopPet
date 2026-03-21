using System;
using System.IO;
using DesktopPet.App.Services;

namespace DesktopPet.Tests.App;

public class PetAnimationCatalogServiceTests
{
    [Fact]
    public void EnsureManifest_MigratesLegacyAssets_ToDefaultCharacter_WithRelativePaths()
    {
        var assetsRoot = Path.Combine(Path.GetTempPath(), "pet_catalog_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(assetsRoot);
        CreateLegacySequence(assetsRoot, "idleRes", "idle1");
        CreateLegacySequence(assetsRoot, "interactRes", "interact1");
        CreateLegacySequence(assetsRoot, "speakRes", "speak1");
        File.WriteAllText(Path.Combine(assetsRoot, "idle.png"), "x");

        try
        {
            var service = new PetAnimationCatalogService(assetsRoot);
            var manifest = service.EnsureManifest();

            Assert.Equal("dog_default", manifest.DefaultCharacterId);
            var character = Assert.Single(manifest.Characters);
            Assert.Equal("dog_default", character.Id);
            Assert.All(manifest.Items, item => Assert.False(Path.IsPathRooted(item.RelativePath)));
            Assert.True(Directory.Exists(Path.Combine(assetsRoot, "Characters", "dog_default", "idle", "idle1")));
            Assert.True(File.Exists(Path.Combine(assetsRoot, "Characters", "dog_default", "character.json")));
        }
        finally
        {
            if (Directory.Exists(assetsRoot))
            {
                Directory.Delete(assetsRoot, recursive: true);
            }
        }
    }

    private static void CreateLegacySequence(string assetsRoot, string categoryRoot, string sequenceName)
    {
        var directory = Path.Combine(assetsRoot, categoryRoot, sequenceName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "0001.png"), "x");
    }
}
