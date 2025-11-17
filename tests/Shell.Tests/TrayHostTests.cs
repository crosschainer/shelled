using Shell.Core;
using Shell.Core.Models;
using Xunit;

namespace Shell.Tests;

/// <summary>
/// Tests for ITrayHost interface functionality
/// </summary>
public class TrayHostTests
{
    private readonly MockTrayHost _trayHost;

    public TrayHostTests()
    {
        _trayHost = new MockTrayHost();
    }

    [Fact]
    public void TrayHost_ShowBalloonNotification_UpdatesTrayIcon()
    {
        // Arrange
        var trayIcon = new TrayIcon { Id = "test-icon", ProcessId = 1234 };
        _trayHost.SimulateTrayIconAdded(trayIcon);

        // Act
        _trayHost.ShowBalloonNotification("test-icon", "Test Title", "Test Message", TrayBalloonIcon.Info, 3000);

        // Assert
        var updatedIcon = _trayHost.GetTrayIcons().First(t => t.Id == "test-icon");
        Assert.NotNull(updatedIcon.BalloonInfo);
        Assert.Equal("Test Title", updatedIcon.BalloonInfo.Title);
        Assert.Equal("Test Message", updatedIcon.BalloonInfo.Text);
        Assert.Equal(TrayBalloonIcon.Info, updatedIcon.BalloonInfo.Icon);
        Assert.Equal(3000, updatedIcon.BalloonInfo.TimeoutMs);
        Assert.NotNull(updatedIcon.BalloonInfo.ShowTime);
    }

    [Fact]
    public void TrayHost_ShowBalloonNotification_WithDefaultValues_Works()
    {
        // Arrange
        var trayIcon = new TrayIcon { Id = "test-icon", ProcessId = 1234 };
        _trayHost.SimulateTrayIconAdded(trayIcon);

        // Act
        _trayHost.ShowBalloonNotification("test-icon", "Title", "Message");

        // Assert
        var updatedIcon = _trayHost.GetTrayIcons().First(t => t.Id == "test-icon");
        Assert.NotNull(updatedIcon.BalloonInfo);
        Assert.Equal("Title", updatedIcon.BalloonInfo.Title);
        Assert.Equal("Message", updatedIcon.BalloonInfo.Text);
        Assert.Equal(TrayBalloonIcon.None, updatedIcon.BalloonInfo.Icon);
        Assert.Equal(5000, updatedIcon.BalloonInfo.TimeoutMs);
    }

    [Fact]
    public void TrayHost_ShowBalloonNotification_NonExistentIcon_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        var exception = Record.Exception(() => 
            _trayHost.ShowBalloonNotification("non-existent", "Title", "Message"));
        
        Assert.Null(exception);
    }

    [Fact]
    public void TrayHost_UpdateTrayIconMenu_UpdatesMenu()
    {
        // Arrange
        var trayIcon = new TrayIcon { Id = "test-icon", ProcessId = 1234 };
        _trayHost.SimulateTrayIconAdded(trayIcon);

        var menu = new TrayMenu
        {
            Items = 
            {
                new TrayMenuItem { Id = "open", Text = "Open", IsEnabled = true },
                new TrayMenuItem { Id = "sep", IsSeparator = true },
                new TrayMenuItem { Id = "exit", Text = "Exit", IsEnabled = true }
            }
        };

        // Act
        _trayHost.UpdateTrayIconMenu("test-icon", menu);

        // Assert
        var updatedIcon = _trayHost.GetTrayIcons().First(t => t.Id == "test-icon");
        Assert.NotNull(updatedIcon.Menu);
        Assert.Equal(3, updatedIcon.Menu.Items.Count);
        Assert.Equal("open", updatedIcon.Menu.Items[0].Id);
        Assert.Equal("Open", updatedIcon.Menu.Items[0].Text);
        Assert.True(updatedIcon.Menu.Items[0].IsEnabled);
        Assert.True(updatedIcon.Menu.Items[1].IsSeparator);
        Assert.Equal("exit", updatedIcon.Menu.Items[2].Id);
    }

    [Fact]
    public void TrayHost_UpdateTrayIconMenu_NonExistentIcon_DoesNotThrow()
    {
        // Arrange
        var menu = new TrayMenu();

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => 
            _trayHost.UpdateTrayIconMenu("non-existent", menu));
        
        Assert.Null(exception);
    }

    [Fact]
    public void TrayHost_TrayMenuItemClicked_EventFires()
    {
        // Arrange
        var eventFired = false;
        string? capturedTrayIconId = null;
        string? capturedMenuItemId = null;

        _trayHost.TrayMenuItemClicked += (trayIconId, menuItemId) =>
        {
            eventFired = true;
            capturedTrayIconId = trayIconId;
            capturedMenuItemId = menuItemId;
        };

        // Act
        _trayHost.SimulateTrayMenuItemClicked("test-icon", "menu-item");

        // Assert
        Assert.True(eventFired);
        Assert.Equal("test-icon", capturedTrayIconId);
        Assert.Equal("menu-item", capturedMenuItemId);
    }

    [Fact]
    public void TrayHost_AllEvents_CanBeSubscribed()
    {
        // Arrange
        var trayIconAddedFired = false;
        var trayIconUpdatedFired = false;
        var trayIconRemovedFired = false;
        var trayIconClickedFired = false;
        var trayMenuItemClickedFired = false;

        // Act - Subscribe to all events
        _trayHost.TrayIconAdded += _ => trayIconAddedFired = true;
        _trayHost.TrayIconUpdated += _ => trayIconUpdatedFired = true;
        _trayHost.TrayIconRemoved += _ => trayIconRemovedFired = true;
        _trayHost.TrayIconClicked += (_, _) => trayIconClickedFired = true;
        _trayHost.TrayMenuItemClicked += (_, _) => trayMenuItemClickedFired = true;

        // Assert - Events should be subscribable without throwing
        Assert.False(trayIconAddedFired);
        Assert.False(trayIconUpdatedFired);
        Assert.False(trayIconRemovedFired);
        Assert.False(trayIconClickedFired);
        Assert.False(trayMenuItemClickedFired);
    }

    [Fact]
    public void TrayHost_ComplexMenu_WithSubItems_Works()
    {
        // Arrange
        var trayIcon = new TrayIcon { Id = "test-icon", ProcessId = 1234 };
        _trayHost.SimulateTrayIconAdded(trayIcon);

        var menu = new TrayMenu
        {
            Items = 
            {
                new TrayMenuItem 
                { 
                    Id = "file", 
                    Text = "File",
                    SubItems = 
                    {
                        new TrayMenuItem { Id = "new", Text = "New" },
                        new TrayMenuItem { Id = "open", Text = "Open" },
                        new TrayMenuItem { Id = "sep1", IsSeparator = true },
                        new TrayMenuItem { Id = "exit", Text = "Exit" }
                    }
                },
                new TrayMenuItem { Id = "help", Text = "Help" }
            }
        };

        // Act
        _trayHost.UpdateTrayIconMenu("test-icon", menu);

        // Assert
        var updatedIcon = _trayHost.GetTrayIcons().First(t => t.Id == "test-icon");
        Assert.NotNull(updatedIcon.Menu);
        Assert.Equal(2, updatedIcon.Menu.Items.Count);
        
        var fileMenu = updatedIcon.Menu.Items[0];
        Assert.Equal("file", fileMenu.Id);
        Assert.Equal("File", fileMenu.Text);
        Assert.Equal(4, fileMenu.SubItems.Count);
        Assert.Equal("new", fileMenu.SubItems[0].Id);
        Assert.Equal("New", fileMenu.SubItems[0].Text);
        Assert.True(fileMenu.SubItems[2].IsSeparator);
        Assert.Equal("exit", fileMenu.SubItems[3].Id);
    }
}