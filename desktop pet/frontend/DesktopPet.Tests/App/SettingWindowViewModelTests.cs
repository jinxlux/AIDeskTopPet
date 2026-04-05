using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using DesktopPet.App.Messaging;
using DesktopPet.App.Services;
using DesktopPet.App.ViewModels;

namespace DesktopPet.Tests.App;

public class SettingWindowViewModelTests
{
    [Fact]
    public void DeleteSelectedAnimationCommand_Disables_ForBuiltInItem()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { CharacterId = "dog_default", ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
            new AnimationAssetEntry { CharacterId = "dog_default", ActionType = "idle", Name = "idle-custom", IsBuiltIn = false },
        ]);
        var vm = CreateViewModel(catalog);

        vm.SelectedCharacter = Assert.Single(vm.CharacterItems, x => x.Id == "dog_default");
        vm.SelectedAnimation = Assert.Single(vm.AnimationItems, x => x.Name == "idle-default");

        Assert.False(vm.DeleteSelectedAnimationCommand.CanExecute(null));
    }

    [Fact]
    public async Task ImportVideoCommand_Imports_AndPublishesMessage()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { CharacterId = "dog_default", ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
        ]);
        var import = new FakeImportService();
        var dialog = new FakeDialogService { PickedPath = @"D:\video\dog.mp4" };
        var messenger = new WeakReferenceMessenger();
        var messageReceived = false;
        messenger.Register<AssetsChangedMessage>(this, (_, _) => messageReceived = true);

        var vm = new SettingWindowViewModel(catalog, import, dialog, messenger);
        vm.SelectedCharacter = Assert.Single(vm.CharacterItems, x => x.Id == "dog_default");
        vm.BrowseVideoCommand.Execute(null);
        vm.SequenceName = "idle-custom-a";
        vm.FpsText = "12";

        await vm.ImportVideoCommand.ExecuteAsync(null);

        Assert.NotNull(import.LastRequest);
        Assert.Equal("dog_default", import.LastRequest!.CharacterId);
        Assert.Equal("idle", import.LastRequest.ActionType);
        Assert.Equal("idle-custom-a", import.LastRequest.SequenceName);
        Assert.True(messageReceived);
        Assert.Equal("导入完成", vm.ImportStatusText);
    }

    [Fact]
    public async Task ImportVideoCommand_Rejects_BuiltInName()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { CharacterId = "dog_default", ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
        ]);
        var import = new FakeImportService();
        var dialog = new FakeDialogService { PickedPath = @"D:\video\dog.mp4" };
        var vm = CreateViewModel(catalog, import, dialog);

        vm.SelectedCharacter = Assert.Single(vm.CharacterItems, x => x.Id == "dog_default");
        vm.BrowseVideoCommand.Execute(null);
        vm.SequenceName = "idle-default";
        vm.FpsText = "12";

        await vm.ImportVideoCommand.ExecuteAsync(null);

        Assert.Null(import.LastRequest);
        Assert.Contains("导入失败", vm.ImportStatusText);
        Assert.Contains("默认动画名称不可复用", dialog.LastError ?? string.Empty);
    }

    [Fact]
    public void SetCurrentCharacterCommand_PublishesCharacterMessage()
    {
        var catalog = new FakeCatalogService(
        [
            new AnimationAssetEntry { CharacterId = "dog_default", ActionType = "idle", Name = "idle-default", IsBuiltIn = true },
        ]);
        var messenger = new WeakReferenceMessenger();
        string? received = null;
        messenger.Register<CurrentCharacterChangedMessage>(this, (_, m) => received = m.Value);
        var vm = new SettingWindowViewModel(catalog, new FakeImportService(), new FakeDialogService(), messenger);

        vm.SelectedCharacter = Assert.Single(vm.CharacterItems);
        vm.SetCurrentCharacterCommand.Execute(null);

        Assert.Equal("dog_default", received);
    }

    private static SettingWindowViewModel CreateViewModel(
        FakeCatalogService catalog,
        FakeImportService? import = null,
        FakeDialogService? dialog = null)
    {
        return new SettingWindowViewModel(
            catalog,
            import ?? new FakeImportService(),
            dialog ?? new FakeDialogService(),
            new WeakReferenceMessenger());
    }

    private sealed class FakeCatalogService : IPetAnimationCatalogService
    {
        private readonly List<AnimationAssetEntry> _items;
        private readonly List<PetCharacterAssetEntry> _characters;

        public FakeCatalogService(List<AnimationAssetEntry> items)
        {
            _items = items;
            _characters =
            [
                new PetCharacterAssetEntry
                {
                    Id = DefaultCharacterId,
                    DisplayName = "默认形象",
                    IsBuiltIn = true,
                    RelativeRootPath = @"Characters\dog_default",
                    Items = [.. items],
                },
            ];
        }

        public string DefaultCharacterId => "dog_default";

        public AnimationAssetManifest EnsureManifest() => new()
        {
            DefaultCharacterId = DefaultCharacterId,
            Items = [.. _items],
            Characters = [.. _characters],
        };

        public IReadOnlyList<AnimationAssetEntry> ListEntries() => [.. _items];
        public IReadOnlyList<PetCharacterAssetEntry> ListCharacters() => [.. _characters];

        public PetCharacterAssetEntry? GetCharacter(string characterId)
            => _characters.FirstOrDefault(x => string.Equals(x.Id, characterId, StringComparison.OrdinalIgnoreCase));

        public void UpdateCharacterProfile(PetCharacterAssetEntry character)
        {
            var existing = _characters.First(x => string.Equals(x.Id, character.Id, StringComparison.OrdinalIgnoreCase));
            existing.DisplayName = character.DisplayName;
            existing.Species = character.Species;
            existing.PersonalityKeywords = [.. character.PersonalityKeywords];
            existing.OwnerCall = character.OwnerCall;
            existing.SelfCall = character.SelfCall;
            existing.IdentityPrompt = character.IdentityPrompt;
            existing.IdlePhrases = [.. character.IdlePhrases];
            existing.InteractPhrases = [.. character.InteractPhrases];
        }

        public bool CharacterExists(string characterId) => _characters.Exists(x => string.Equals(x.Id, characterId, StringComparison.OrdinalIgnoreCase));

        public string CreateCharacter(string displayName, string? species = null, IEnumerable<string>? personalityKeywords = null)
        {
            var id = displayName;
            _characters.Add(new PetCharacterAssetEntry
            {
                Id = id,
                DisplayName = displayName,
                Species = species ?? string.Empty,
                PersonalityKeywords = personalityKeywords?.ToList() ?? [],
                RelativeRootPath = $@"Characters\{id}",
            });
            return id;
        }

        public string RenameCharacter(string characterId, string newDisplayName)
        {
            var item = _characters.Find(x => x.Id == characterId)!;
            item.Id = newDisplayName;
            item.DisplayName = newDisplayName;
            return newDisplayName;
        }

        public bool DeleteCustomCharacter(string characterId)
        {
            return _characters.RemoveAll(x => x.Id == characterId && !x.IsBuiltIn) > 0;
        }

        public bool SequenceExists(string actionType, string sequenceName) => SequenceExists(DefaultCharacterId, actionType, sequenceName);

        public bool SequenceExists(string characterId, string actionType, string sequenceName)
            => _items.Exists(x => x.CharacterId == characterId && x.ActionType == actionType && x.Name == sequenceName);

        public bool IsBuiltInSequence(string actionType, string sequenceName) => IsBuiltInSequence(DefaultCharacterId, actionType, sequenceName);

        public bool IsBuiltInSequence(string characterId, string actionType, string sequenceName)
            => _items.Exists(x => x.CharacterId == characterId && x.ActionType == actionType && x.Name == sequenceName && x.IsBuiltIn);

        public bool DeleteCustomSequence(string actionType, string sequenceName) => DeleteCustomSequence(DefaultCharacterId, actionType, sequenceName);

        public bool DeleteCustomSequence(string characterId, string actionType, string sequenceName)
        {
            var idx = _items.FindIndex(x => x.CharacterId == characterId && x.ActionType == actionType && x.Name == sequenceName && !x.IsBuiltIn);
            if (idx < 0)
            {
                return false;
            }

            _items.RemoveAt(idx);
            return true;
        }

        public void RestoreBuiltInDefaults()
        {
            _items.RemoveAll(x => !x.IsBuiltIn);
            _characters.RemoveAll(x => !x.IsBuiltIn);
        }
    }

    private sealed class FakeImportService : IVideoAnimationImportService
    {
        public VideoAnimationImportRequest? LastRequest { get; private set; }

        public Task<string> ImportAsync(VideoAnimationImportRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(@"D:\out");
        }
    }

    private sealed class FakeDialogService : ISettingsDialogService
    {
        public string? PickedPath { get; set; }
        public string? LastError { get; private set; }
        public Queue<string?> PromptResults { get; } = new();

        public string? PickVideoFilePath() => PickedPath;

        public string? PromptText(string title, string message, string? defaultValue = null)
            => PromptResults.Count > 0 ? PromptResults.Dequeue() : defaultValue;

        public void ShowInfo(string message, string title = "提示") { }
        public void ShowError(string message, string title = "错误") => LastError = message;
        public bool Confirm(string message, string title) => true;
    }
}


