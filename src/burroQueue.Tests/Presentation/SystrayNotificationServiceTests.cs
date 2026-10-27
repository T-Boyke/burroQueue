using burroQueue.Presentation.Services;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class SystrayNotificationServiceTests
{
    [Fact]
    public void ShowNotification_BeforeInitialize_ShouldNotThrow()
    {
        SystrayNotificationService service = new();

        Action act = () => service.ShowNotification("Title", "Message");

        act.Should().NotThrow();
    }

    [Fact]
    public void Initialize_WithNullMainWindow_ShouldThrowArgumentNullException()
    {
        SystrayNotificationService service = new();

        Action act = () => service.Initialize(null!, () => { }, () => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InitializeCore_ShouldSetupMenuAndHandleClicks()
    {
        SystrayNotificationService service = new();
        bool restored = false;
        bool settingsOpened = false;
        bool exitCalled = false;

        service.InitializeCore(
            onRestoreWindow: () => restored = true,
            onOpenSettings: () => settingsOpened = true,
            onExitApp: () => exitCalled = true);

        service.IsIconVisible.Should().BeTrue();
        service.ContextMenu.Should().NotBeNull();
        service.ContextMenu!.Items.Count.Should().Be(4);

        // Click "burroQueue anzeigen"
        service.ContextMenu.Items[0].PerformClick();
        restored.Should().BeTrue();

        // Click "Einstellungen..."
        service.ContextMenu.Items[1].PerformClick();
        settingsOpened.Should().BeTrue();

        // Click "Beenden"
        service.ContextMenu.Items[3].PerformClick();
        exitCalled.Should().BeTrue();

        service.Dispose();
        service.IsIconVisible.Should().BeFalse();
    }

    [Fact]
    public void InitializeCore_WhenCustomIconExists_ShouldLoadIcon()
    {
        SystrayNotificationService service = new();
        string tempIcon = Path.GetTempFileName() + ".ico";
        try
        {
            using (System.Drawing.Bitmap bmp = new(16, 16))
            {
                IntPtr hIcon = bmp.GetHicon();
                using System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(hIcon);
                using FileStream fs = File.OpenWrite(tempIcon);
                icon.Save(fs);
            }

            service.InitializeCore(() => { }, () => { }, () => { }, tempIcon);
            service.IsIconVisible.Should().BeTrue();
        }
        finally
        {
            service.Dispose();
            if (File.Exists(tempIcon))
            {
                try { File.Delete(tempIcon); } catch { }
            }
        }
    }

    [Fact]
    public void InitializeCore_WhenIconCorrupt_ShouldFallbackToApplicationIcon()
    {
        SystrayNotificationService service = new();
        string tempCorrupt = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCorrupt, "not an icon file format");

            service.InitializeCore(() => { }, () => { }, () => { }, tempCorrupt);
            service.IsIconVisible.Should().BeTrue();
        }
        finally
        {
            service.Dispose();
            if (File.Exists(tempCorrupt))
            {
                try { File.Delete(tempCorrupt); } catch { }
            }
        }
    }

    [Fact]
    public void Initialize_WithValidWindow_ShouldInitializeSuccessfully()
    {
        SystrayNotificationService service = new();
        burroQueue.Presentation.Views.MainWindow dummyWindow = (burroQueue.Presentation.Views.MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(burroQueue.Presentation.Views.MainWindow));
        bool restored = false;
        Action<burroQueue.Presentation.Views.MainWindow> original = SystrayNotificationService.WindowRestorer;
        try
        {
            SystrayNotificationService.WindowRestorer = w => restored = w == dummyWindow;

            service.Initialize(dummyWindow, () => { }, () => { });

            service.IsIconVisible.Should().BeTrue();
            service.ContextMenu!.Items[0].PerformClick();
            restored.Should().BeTrue();
        }
        finally
        {
            SystrayNotificationService.WindowRestorer = original;
            service.Dispose();
        }
    }

    [Fact]
    public void ShowNotification_WhenInitialized_ShouldNotThrow()
    {
        SystrayNotificationService service = new();
        service.InitializeCore(() => { }, () => { }, () => { });

        Action act = () => service.ShowNotification("Office Jukebox", "Song playing");

        act.Should().NotThrow();
        service.Dispose();
    }

    [Fact]
    public void Dispose_WhenNotInitialized_ShouldBeIdempotentAndNotThrow()
    {
        SystrayNotificationService service = new();

        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        act.Should().NotThrow();
    }
}
