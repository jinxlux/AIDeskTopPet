using DesktopPet.Core.Models;
using DesktopPet.Infrastructure;

namespace DesktopPet.Tests.Infrastructure;

public sealed class JsonSettingsServiceTests
{
    [Fact]
    public void Load_ShouldReturnDefaults_WhenFileMissing()
    {
        var path = BuildTempSettingsPath();
        var service = new JsonSettingsService(path);

        var settings = service.Load();

        Assert.Equal(80, settings.Left);
        Assert.Equal(80, settings.Top);
        Assert.True(settings.PreferSmoothRendering);
    }

    [Fact]
    public void SaveThenLoad_ShouldRoundTripValues()
    {
        var path = BuildTempSettingsPath();
        var service = new JsonSettingsService(path);

        service.Save(new PetSettings { Left = 320, Top = 240, PreferSmoothRendering = false });
        var loaded = service.Load();

        Assert.Equal(320, loaded.Left);
        Assert.Equal(240, loaded.Top);
        Assert.False(loaded.PreferSmoothRendering);

        Cleanup(path);
    }

    [Fact]
    public void Load_ShouldReturnDefaults_WhenJsonCorrupted()
    {
        var path = BuildTempSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this-is-invalid-json }");

        var service = new JsonSettingsService(path);
        var loaded = service.Load();

        Assert.Equal(80, loaded.Left);
        Assert.Equal(80, loaded.Top);
        Assert.True(loaded.PreferSmoothRendering);

        Cleanup(path);
    }

    private static string BuildTempSettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), "DesktopPet.Tests", Guid.NewGuid().ToString("N"), "settings.json");
    }

    private static void Cleanup(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
