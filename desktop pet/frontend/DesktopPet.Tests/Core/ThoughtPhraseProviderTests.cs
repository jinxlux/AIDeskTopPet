using DesktopPet.Core.Services;

namespace DesktopPet.Tests.Core;

public sealed class ThoughtPhraseProviderTests
{
    [Fact]
    public void GetRandomPhrase_ShouldReturnNonEmptyPhrase()
    {
        var provider = new ThoughtPhraseProvider();

        var phrase = provider.GetRandomPhrase(new Random(0));

        Assert.False(string.IsNullOrWhiteSpace(phrase));
    }

    [Fact]
    public void GetRandomPhrase_ShouldAvoidImmediateRepeat_WhenPossible()
    {
        var provider = new ThoughtPhraseProvider(["A", "B", "C"], recentWindow: 2);
        var random = new Random(1);

        var first = provider.GetRandomPhrase(random);
        var second = provider.GetRandomPhrase(random);

        Assert.NotEqual(first, second);
    }
}
