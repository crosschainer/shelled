using Shell.Core;
using Shell.Core.Models;
using Xunit;

namespace Shell.Tests;

/// <summary>
/// Tests for tray icon models and functionality
/// </summary>
public class TrayModelTests
{
    [Fact]
    public void TrayIcon_DefaultValues_AreSetCorrectly()
    {
        // Act
        var trayIcon = new TrayIcon();

        // Assert
        Assert.Equal(string.Empty, trayIcon.Id);
        Assert.Equal(0, trayIcon.ProcessId);
        Assert.Equal(string.Empty, trayIcon.Tooltip);
        Assert.Null(trayIcon.IconData);
        Assert.Equal(IntPtr.Zero, trayIcon.IconHandle);
        Assert.True(trayIcon.IsVisible);
        Assert.Null(trayIcon.Menu);
        Assert.Null(trayIcon.BalloonInfo);
        Assert.True(trayIcon.CreatedAt <= DateTime.UtcNow);
        Assert.True(trayIcon.LastUpdated <= DateTime.UtcNow);
    }

    [Fact]
    public void TrayMenu_CanBeCreatedWithItems()
    {
        // Arrange
        var menuItem1 = new TrayMenuItem
        {
            Id = "item1",
            Text = "Item 1",
            IsEnabled = true
        };
        var menuItem2 = new TrayMenuItem
        {
            Id = "item2",
            Text = "Item 2",
            IsEnabled = false,
            IsChecked = true
        };
        var separator = new TrayMenuItem
        {
            Id = "sep1",
            IsSeparator = true
        };

        // Act
        var menu = new TrayMenu
        {
            Items = { menuItem1, menuItem2, separator }
        };

        // Assert
        Assert.Equal(3, menu.Items.Count);
        Assert.Equal("item1", menu.Items[0].Id);
        Assert.Equal("Item 1", menu.Items[0].Text);
        Assert.True(menu.Items[0].IsEnabled);
        Assert.False(menu.Items[0].IsChecked);
        Assert.False(menu.Items[0].IsSeparator);

        Assert.Equal("item2", menu.Items[1].Id);
        Assert.Equal("Item 2", menu.Items[1].Text);
        Assert.False(menu.Items[1].IsEnabled);
        Assert.True(menu.Items[1].IsChecked);
        Assert.False(menu.Items[1].IsSeparator);

        Assert.Equal("sep1", menu.Items[2].Id);
        Assert.True(menu.Items[2].IsSeparator);
    }

    [Fact]
    public void TrayMenuItem_CanHaveSubItems()
    {
        // Arrange
        var subItem1 = new TrayMenuItem { Id = "sub1", Text = "Sub Item 1" };
        var subItem2 = new TrayMenuItem { Id = "sub2", Text = "Sub Item 2" };

        // Act
        var parentItem = new TrayMenuItem
        {
            Id = "parent",
            Text = "Parent Item",
            SubItems = { subItem1, subItem2 }
        };

        // Assert
        Assert.Equal(2, parentItem.SubItems.Count);
        Assert.Equal("sub1", parentItem.SubItems[0].Id);
        Assert.Equal("Sub Item 1", parentItem.SubItems[0].Text);
        Assert.Equal("sub2", parentItem.SubItems[1].Id);
        Assert.Equal("Sub Item 2", parentItem.SubItems[1].Text);
    }

    [Fact]
    public void TrayBalloonInfo_DefaultValues_AreSetCorrectly()
    {
        // Act
        var balloonInfo = new TrayBalloonInfo();

        // Assert
        Assert.Equal(string.Empty, balloonInfo.Title);
        Assert.Equal(string.Empty, balloonInfo.Text);
        Assert.Equal(TrayBalloonIcon.None, balloonInfo.Icon);
        Assert.Equal(5000, balloonInfo.TimeoutMs);
        Assert.Null(balloonInfo.ShowTime);
    }

    [Fact]
    public void TrayBalloonInfo_CanBeConfigured()
    {
        // Arrange
        var showTime = DateTime.UtcNow;

        // Act
        var balloonInfo = new TrayBalloonInfo
        {
            Title = "Test Title",
            Text = "Test message",
            Icon = TrayBalloonIcon.Warning,
            TimeoutMs = 10000,
            ShowTime = showTime
        };

        // Assert
        Assert.Equal("Test Title", balloonInfo.Title);
        Assert.Equal("Test message", balloonInfo.Text);
        Assert.Equal(TrayBalloonIcon.Warning, balloonInfo.Icon);
        Assert.Equal(10000, balloonInfo.TimeoutMs);
        Assert.Equal(showTime, balloonInfo.ShowTime);
    }

    [Theory]
    [InlineData(TrayBalloonIcon.None)]
    [InlineData(TrayBalloonIcon.Info)]
    [InlineData(TrayBalloonIcon.Warning)]
    [InlineData(TrayBalloonIcon.Error)]
    public void TrayBalloonIcon_AllValuesAreValid(TrayBalloonIcon icon)
    {
        // Act & Assert - Should not throw
        var balloonInfo = new TrayBalloonInfo { Icon = icon };
        Assert.Equal(icon, balloonInfo.Icon);
    }

    [Fact]
    public void TrayIcon_WithCompleteConfiguration_WorksCorrectly()
    {
        // Arrange
        var menu = new TrayMenu
        {
            Items = 
            {
                new TrayMenuItem { Id = "open", Text = "Open" },
                new TrayMenuItem { Id = "sep", IsSeparator = true },
                new TrayMenuItem { Id = "exit", Text = "Exit" }
            }
        };

        var balloonInfo = new TrayBalloonInfo
        {
            Title = "Notification",
            Text = "Test notification",
            Icon = TrayBalloonIcon.Info,
            ShowTime = DateTime.UtcNow
        };

        // Act
        var trayIcon = new TrayIcon
        {
            Id = "test-app",
            ProcessId = 1234,
            Tooltip = "Test Application",
            IconData = new byte[] { 1, 2, 3, 4 },
            IconHandle = new IntPtr(5678),
            Menu = menu,
            BalloonInfo = balloonInfo
        };

        // Assert
        Assert.Equal("test-app", trayIcon.Id);
        Assert.Equal(1234, trayIcon.ProcessId);
        Assert.Equal("Test Application", trayIcon.Tooltip);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, trayIcon.IconData);
        Assert.Equal(new IntPtr(5678), trayIcon.IconHandle);
        Assert.NotNull(trayIcon.Menu);
        Assert.Equal(3, trayIcon.Menu.Items.Count);
        Assert.NotNull(trayIcon.BalloonInfo);
        Assert.Equal("Notification", trayIcon.BalloonInfo.Title);
    }
}