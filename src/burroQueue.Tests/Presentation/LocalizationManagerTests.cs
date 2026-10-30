using System.Globalization;
using burroQueue.Presentation.Localization;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class LocalizationManagerTests
{
    [Fact]
    public void SetLanguage_ShouldSwitchBetweenGermanAndEnglishLiveAndNotifyIndexerChanged()
    {
        LocalizationManager manager = LocalizationManager.Instance;
        int changeNotifications = 0;
        manager.PropertyChanged += (_, _) => changeNotifications++;

        manager.SetLanguage("en-US");
        manager.CurrentCulture.Name.Should().Be("en-US");
        manager["QueueHeader"].Should().Be("Queue");
        manager["GroupFavoritesHeader"].Should().Be("Group Favorites");
        manager["SettingsTitle"].Should().Be("Settings");

        manager.SetLanguage("de-DE");
        manager.CurrentCulture.Name.Should().Be("de-DE");
        manager["QueueHeader"].Should().Be("Warteschlange");
        manager["GroupFavoritesHeader"].Should().Be("Gruppen-Favoriten");
        manager["SettingsTitle"].Should().Be("Einstellungen");

        changeNotifications.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void SetLanguage_WithAuto_ShouldResolveOsCultureToDeOrEn()
    {
        LocalizationManager manager = LocalizationManager.Instance;

        manager.SetLanguage("Auto", new CultureInfo("de-AT"));
        manager.CurrentCulture.Name.Should().Be("de-DE");

        manager.SetLanguage("Auto", new CultureInfo("fr-FR"));
        manager.CurrentCulture.Name.Should().Be("en-US");
    }
}

