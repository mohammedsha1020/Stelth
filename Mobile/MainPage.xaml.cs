using Stealth.Shared;

#nullable enable

namespace Stealth.Mobile;

public partial class MainPage : ContentPage
{
	private TrustedCodeManager? _trustedCodeManager;
	private MobileConnectionManager? _connectionManager;
	private bool _isConnected = false;

	public MainPage()
	{
		InitializeComponent();
		InitializeAsync();
	}

	private async void InitializeAsync()
	{
		try
		{
			_trustedCodeManager = new TrustedCodeManager();
			_connectionManager = new MobileConnectionManager();
			
			// Setup event handlers
			_connectionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
			_connectionManager.ScreenDataReceived += OnScreenDataReceived;
			_connectionManager.LogMessageReceived += OnLogMessageReceived;
			
			var deviceId = _trustedCodeManager.GetDeviceId();
			DeviceInfoLabel.Text = $"Device ID: {deviceId}";

			var existingCode = _trustedCodeManager.GetMyTrustedCode();
			if (!string.IsNullOrEmpty(existingCode))
			{
				TrustedCodeEntry.Text = existingCode;
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Initialization Error", $"Failed to initialize: {ex.Message}", "OK");
		}
	}

	private async void OnConnectClicked(object sender, EventArgs e)
	{
		try
		{
			if (_isConnected)
			{
				// Disconnect
				await _connectionManager!.DisconnectAsync();
				return;
			}

			var serverIp = ServerIpEntry.Text?.Trim();
			var trustedCode = TrustedCodeEntry.Text?.Trim();

			if (string.IsNullOrEmpty(serverIp))
			{
				await DisplayAlert("Error", "Please enter the PC server IP address", "OK");
				return;
			}

			if (string.IsNullOrEmpty(trustedCode))
			{
				await DisplayAlert("Error", "Please enter the trusted code", "OK");
				return;
			}

			ConnectBtn.IsEnabled = false;
			ConnectBtn.Text = "Connecting...";
			StatusLabel.Text = "Connecting to PC...";

			// Attempt connection
			var port = 7890; // Default port
			var success = await _connectionManager!.ConnectAsync(serverIp, port, trustedCode);

			if (!success)
			{
				await DisplayAlert("Connection Failed", "Could not connect to PC. Please check IP address and trusted code.", "OK");
				ConnectBtn.Text = "Connect to PC";
				ConnectBtn.IsEnabled = true;
				StatusLabel.Text = "Connection failed";
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Connection Error", $"Failed to connect: {ex.Message}", "OK");
			StatusLabel.Text = "Connection failed";
			ConnectBtn.Text = "Connect to PC";
			ConnectBtn.IsEnabled = true;
		}
	}

	private async void OnStartSharingClicked(object sender, EventArgs e)
	{
		if (!_isConnected)
		{
			await DisplayAlert("Error", "Please connect to PC first", "OK");
			return;
		}

		try
		{
			await _connectionManager!.RequestScreenSharingAsync(true);
			await DisplayAlert("Screen Sharing", "Requested PC to start screen sharing", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to start screen sharing: {ex.Message}", "OK");
		}
	}

	private async void OnViewRemoteClicked(object sender, EventArgs e)
	{
		if (!_isConnected)
		{
			await DisplayAlert("Error", "Please connect to PC first", "OK");
			return;
		}

		try
		{
			// Navigate to screen viewing page
			var screenViewPage = new ScreenViewPage(_connectionManager!);
			await Navigation.PushAsync(screenViewPage);
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to start remote view: {ex.Message}", "OK");
		}
	}

	private void OnConnectionStatusChanged(object? sender, ConnectionStatusEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_isConnected = e.IsConnected;
			
			if (e.IsConnected)
			{
				StatusLabel.Text = $"Connected to {e.ServerAddress}";
				ConnectBtn.Text = "Disconnect";
				ConnectBtn.IsEnabled = true;
			}
			else
			{
				StatusLabel.Text = "Disconnected";
				ConnectBtn.Text = "Connect to PC";
				ConnectBtn.IsEnabled = true;
			}
		});
	}

	private void OnScreenDataReceived(object? sender, ScreenDataReceivedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			// TODO: Display the received screen data
			// For now, just update status
			StatusLabel.Text = $"Receiving screen data ({e.ScreenData.Width}x{e.ScreenData.Height})";
		});
	}

	private void OnLogMessageReceived(object? sender, string message)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			// TODO: Add to logs page
			System.Diagnostics.Debug.WriteLine($"[Mobile] {message}");
		});
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_connectionManager?.Dispose();
	}
}
