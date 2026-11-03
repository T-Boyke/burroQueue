using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows;
using Wpf.Ui.Controls;

namespace burroQueue.Presentation.Views;

[ExcludeFromCodeCoverage]
public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Wpf.Ui.Appearance.ApplicationAccentColorManager.ApplySystemAccent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);

            var version = typeof(App).Assembly.GetName().Version;
            var infoVersion = typeof(App).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            string displayVersion = !string.IsNullOrWhiteSpace(infoVersion)
                ? infoVersion.Split('+')[0]
                : (version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2.3.2");
            VersionTextBlock.Text = $"Version {displayVersion} (Release x64 · .NET 11)";
        };
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
