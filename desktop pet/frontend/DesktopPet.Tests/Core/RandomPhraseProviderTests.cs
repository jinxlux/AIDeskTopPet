using DesktopPet.Core.Services;

namespace DesktopPet.Tests.Core;

public sealed class RandomPhraseProviderTests
{
    [Fact]
    public void GetRandomPhrase_ShouldReturnNonEmpty_ForDefaultList()
    {
        var provider = new RandomPhraseProvider();

        var phrase = provider.GetRandomPhrase(new Random(0));

        Assert.False(string.IsNullOrWhiteSpace(phrase));
    }

    [Fact]
    public void GetRandomPhrase_ShouldPickFromCustomList()
    {
        var provider = new RandomPhraseProvider(["A", "B"]);

        var phrase = provider.GetRandomPhrase(new Random(1));

        Assert.Contains(phrase, new[] { "A", "B" });
    }
}
