using Stealth.Shared;

namespace Stealth.Mobile;

public partial class ScreenViewPage : ContentPage
{
    private MobileConnectionManager? _connectionManager;
    private bool _isMouseMode = true;
    private DateTime _lastTapTime = DateTime.MinValue;
    private const double DoubleTapThreshold = 300; // milliseconds

    public ScreenViewPage(MobileConnectionManager connectionManager)
    {
        InitializeComponent();
        _connectionManager = connectionManager;
        
        // Subscribe to screen data events
        if (_connectionManager != null)
        {
            _connectionManager.ScreenDataReceived += OnScreenDataReceived;
            _connectionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        }
        
        // Hide loading overlay initially
        LoadingOverlay.IsVisible = false;
        
        // Request screen sharing to start
        Task.Run(async () =>
        {
            if (_connectionManager != null)
            {
                await _connectionManager.RequestScreenSharingAsync(true);
            }
        });
    }

    private void OnScreenDataReceived(object? sender, ScreenDataReceivedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (e.ScreenData.ImageData != null)
                {
                    var imageStream = new MemoryStream(e.ScreenData.ImageData);
                    ScreenImage.Source = ImageSource.FromStream(() => imageStream);
                    
                    // Update screen dimensions
                    ScreenImage.WidthRequest = e.ScreenData.Width;
                    ScreenImage.HeightRequest = e.ScreenData.Height;
                    
                    // Hide loading overlay
                    LoadingOverlay.IsVisible = false;
                    
                    // Update status
                    StatusLabel.Text = $"{e.ScreenData.Width}x{e.ScreenData.Height}";
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to display screen: {ex.Message}", "OK");
            }
        });
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!e.IsConnected)
            {
                DisplayAlert("Disconnected", "Connection to PC lost", "OK");
                Navigation.PopAsync();
            }
        });
    }

    private async void OnScreenTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_connectionManager == null) return;

            var position = e.GetPosition(ScreenImage);
            if (position == null) return;

            // Convert tap position to screen coordinates
            var x = (float)(position.Value.X * ScreenImage.WidthRequest / ScreenImage.Width);
            var y = (float)(position.Value.Y * ScreenImage.HeightRequest / ScreenImage.Height);

            // Check for double-tap
            var now = DateTime.Now;
            var isDoubleTap = (now - _lastTapTime).TotalMilliseconds < DoubleTapThreshold;
            _lastTapTime = now;

            if (_isMouseMode)
            {
                // Send mouse click
                var inputEvent = new InputEventMessage
                {
                    Type = isDoubleTap ? Protocol.InputType.MouseClick : Protocol.InputType.MouseClick,
                    X = x,
                    Y = y,
                    Button = 0 // Left click
                };

                await _connectionManager.SendInputEventAsync(inputEvent);
            }
            else
            {
                // Send touch tap
                var inputEvent = new InputEventMessage
                {
                    Type = Protocol.InputType.TouchTap,
                    X = x,
                    Y = y,
                    Pressure = 1.0f
                };

                await _connectionManager.SendInputEventAsync(inputEvent);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send tap: {ex.Message}", "OK");
        }
    }

    private async void OnScreenPanned(object sender, PanUpdatedEventArgs e)
    {
        try
        {
            if (_connectionManager == null || e.StatusType != GestureStatus.Running) return;

            // Convert pan delta to screen coordinates
            var deltaX = (float)(e.TotalX * ScreenImage.WidthRequest / ScreenImage.Width);
            var deltaY = (float)(e.TotalY * ScreenImage.HeightRequest / ScreenImage.Height);

            if (_isMouseMode)
            {
                // Send mouse move
                var inputEvent = new InputEventMessage
                {
                    Type = Protocol.InputType.MouseMove,
                    X = deltaX,
                    Y = deltaY
                };

                await _connectionManager.SendInputEventAsync(inputEvent);
            }
            else
            {
                // Send touch drag
                var inputEvent = new InputEventMessage
                {
                    Type = Protocol.InputType.TouchDrag,
                    X = deltaX,
                    Y = deltaY,
                    Pressure = 1.0f
                };

                await _connectionManager.SendInputEventAsync(inputEvent);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send pan: {ex.Message}", "OK");
        }
    }

    private void OnMouseModeClicked(object sender, EventArgs e)
    {
        _isMouseMode = true;
        MouseModeButton.BackgroundColor = Colors.White;
        MouseModeButton.TextColor = Colors.Black;
        TouchModeButton.BackgroundColor = Colors.Transparent;
        TouchModeButton.TextColor = Colors.White;
    }

    private void OnTouchModeClicked(object sender, EventArgs e)
    {
        _isMouseMode = false;
        TouchModeButton.BackgroundColor = Colors.White;
        TouchModeButton.TextColor = Colors.Black;
        MouseModeButton.BackgroundColor = Colors.Transparent;
        MouseModeButton.TextColor = Colors.White;
    }

    private async void OnKeyboardClicked(object sender, EventArgs e)
    {
        try
        {
            // Show virtual keyboard for text input
            var result = await DisplayPromptAsync("Virtual Keyboard", "Enter text to type on PC:", 
                placeholder: "Type here...", maxLength: 500);
            
            if (!string.IsNullOrEmpty(result) && _connectionManager != null)
            {
                // Send each character as a key press
                foreach (char c in result)
                {
                    var inputEvent = new InputEventMessage
                    {
                        Type = Protocol.InputType.KeyPress,
                        KeyChar = c.ToString()
                    };

                    await _connectionManager.SendInputEventAsync(inputEvent);
                    await Task.Delay(50); // Small delay between keystrokes
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send keyboard input: {ex.Message}", "OK");
        }
    }

    private async void OnClipboardClicked(object sender, EventArgs e)
    {
        try
        {
            // Get clipboard content and send as keyboard input
            var clipboardText = await Clipboard.GetTextAsync();
            if (!string.IsNullOrEmpty(clipboardText) && _connectionManager != null)
            {
                foreach (char c in clipboardText)
                {
                    var inputEvent = new InputEventMessage
                    {
                        Type = Protocol.InputType.KeyPress,
                        KeyChar = c.ToString()
                    };

                    await _connectionManager.SendInputEventAsync(inputEvent);
                }
                
                await DisplayAlert("Clipboard", "Clipboard content sent to PC", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send clipboard: {ex.Message}", "OK");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Screen Settings", "Cancel", null, 
            "Request Refresh", "Change Quality", "Disconnect");
        
        switch (action)
        {
            case "Request Refresh":
                await _connectionManager?.RequestScreenSharingAsync(true);
                break;
            case "Change Quality":
                await DisplayAlert("Quality", "Quality settings will be implemented", "OK");
                break;
            case "Disconnect":
                await _connectionManager?.DisconnectAsync();
                break;
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        await _connectionManager?.RequestScreenSharingAsync(true);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Stop screen sharing when leaving the page
        Task.Run(async () =>
        {
            if (_connectionManager != null)
            {
                await _connectionManager.RequestScreenSharingAsync(false);
            }
        });
        
        // Unsubscribe from events
        if (_connectionManager != null)
        {
            _connectionManager.ScreenDataReceived -= OnScreenDataReceived;
            _connectionManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }
    }
}
