using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;
using Stealth.Shared;

namespace Stealth.PC
{
    /// <summary>
    /// Main window for the Stealth PC application
    /// </summary>
    public partial class MainWindow : Window
    {
        private ConnectionManager? _connectionManager;
        private ScreenStreamer? _screenStreamer;
        private InputReceiver? _inputReceiver;
        private TrustedCodeManager? _trustedCodeManager;
        private DispatcherTimer? _statusTimer;
        private readonly ObservableCollection<ConnectedDeviceViewModel> _connectedDevices;
        private bool _isSilentMode;

        public MainWindow(bool silentMode = false)
        {
            InitializeComponent();
            _isSilentMode = silentMode;
            _connectedDevices = new ObservableCollection<ConnectedDeviceViewModel>();
            ConnectedDevicesListView.ItemsSource = _connectedDevices;
            
            InitializeComponents();
            SetupEventHandlers();
            StartStatusTimer();
            
            if (_isSilentMode)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
            }
        }

        private void InitializeComponents()
        {
            _trustedCodeManager = new TrustedCodeManager();
            _connectionManager = new ConnectionManager();
            _screenStreamer = new ScreenStreamer();
            _inputReceiver = new InputReceiver();

            // Setup device ID
            DeviceIdTextBox.Text = _trustedCodeManager.GetDeviceId();
            
            // Setup trusted code
            var existingCode = _trustedCodeManager.GetMyTrustedCode();
            if (!string.IsNullOrEmpty(existingCode))
            {
                TrustedCodeTextBox.Text = existingCode;
            }

            // Setup auto-start checkbox
            AutoStartCheckBox.IsChecked = AutoStartManager.IsAutoStartEnabled();

            // Initialize status
            UpdateStatus("Ready");
        }

        private void SetupEventHandlers()
        {
            if (_connectionManager != null)
            {
                _connectionManager.ClientConnected += OnClientConnected;
                _connectionManager.ClientDisconnected += OnClientDisconnected;
                _connectionManager.MessageReceived += OnMessageReceived;
            }

            if (_screenStreamer != null)
            {
                _screenStreamer.ScreenDataCaptured += OnScreenDataCaptured;
            }

            // Quality slider handler
            QualitySlider.ValueChanged += (s, e) => QualityLabel.Text = $"{(int)e.NewValue}%";
        }

        private void StartStatusTimer()
        {
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += (s, e) => TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            _statusTimer.Start();
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(PortTextBox.Text, out var port))
                {
                    await _connectionManager!.StartServerAsync(port);
                    StartServerButton.IsEnabled = false;
                    UpdateStatus($"Server listening on port {port}");
                    NetworkStatusText.Text = "Listening";
                    AddLog($"Server started on port {port}");
                }
                else
                {
                    MessageBox.Show("Invalid port number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"Failed to start server: {ex.Message}");
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ipAddress = IpAddressTextBox.Text.Trim();
                if (string.IsNullOrEmpty(ipAddress))
                {
                    MessageBox.Show("Please enter an IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ConnectButton.IsEnabled = false;
                ConnectButton.Content = "Connecting...";
                UpdateStatus($"Connecting to {ipAddress}...");

                var port = int.TryParse(PortTextBox.Text, out var p) ? p : 7890;
                var success = await _connectionManager!.ConnectToDeviceAsync(ipAddress, port);

                if (success)
                {
                    UpdateStatus($"Connected to {ipAddress}");
                    NetworkStatusText.Text = "Connected";
                    AddLog($"Successfully connected to {ipAddress}:{port}");
                }
                else
                {
                    UpdateStatus("Connection failed");
                    NetworkStatusText.Text = "Offline";
                    AddLog($"Failed to connect to {ipAddress}:{port}");
                    MessageBox.Show("Failed to connect to device", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Connection error");
                AddLog($"Connection error: {ex.Message}");
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
                ConnectButton.Content = "Connect";
            }
        }

        private void StartSharingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fps = (int)FpsSlider.Value;
                _screenStreamer!.StartStreaming(fps);
                
                StartSharingButton.IsEnabled = false;
                StopSharingButton.IsEnabled = true;
                UpdateStatus("Screen sharing started");
                AddLog($"Screen sharing started at {fps} FPS");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start screen sharing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"Failed to start screen sharing: {ex.Message}");
            }
        }

        private void StopSharingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _screenStreamer!.StopStreaming();
                
                StartSharingButton.IsEnabled = true;
                StopSharingButton.IsEnabled = false;
                UpdateStatus("Screen sharing stopped");
                AddLog("Screen sharing stopped");
            }
            catch (Exception ex)
            {
                AddLog($"Error stopping screen sharing: {ex.Message}");
            }
        }

        private void GenerateCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newCode = _trustedCodeManager!.GenerateNewTrustedCode();
                TrustedCodeTextBox.Text = newCode;
                AddLog("Generated new trusted code");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate trusted code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(DeviceIdTextBox.Text);
                StatusBarText.Text = "Device ID copied to clipboard";
            }
            catch (Exception ex)
            {
                AddLog($"Failed to copy device ID: {ex.Message}");
            }
        }

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TrustedCodeTextBox.Text))
                {
                    Clipboard.SetText(TrustedCodeTextBox.Text);
                    StatusBarText.Text = "Trusted code copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                AddLog($"Failed to copy trusted code: {ex.Message}");
            }
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AutoStartCheckBox.IsChecked == true)
                {
                    if (AutoStartManager.EnableAutoStartBest())
                    {
                        AddLog("Auto-start enabled");
                    }
                    else
                    {
                        AutoStartCheckBox.IsChecked = false;
                        MessageBox.Show("Failed to enable auto-start", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    if (AutoStartManager.DisableAutoStartAll())
                    {
                        AddLog("Auto-start disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Auto-start error: {ex.Message}");
            }
        }

        private void ClearTrustedDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all trusted devices? This will require re-authentication for all devices.",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var devices = _trustedCodeManager!.GetTrustedDevices().ToList();
                    foreach (var device in devices)
                    {
                        _trustedCodeManager.RemoveTrustedDevice(device.DeviceId);
                    }
                    AddLog("All trusted devices cleared");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear trusted devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FpsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FpsLabel != null)
            {
                FpsLabel.Text = ((int)e.NewValue).ToString();
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void ExportLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"stealth_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, LogTextBox.Text);
                    StatusBarText.Text = "Logs exported successfully";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var deviceViewModel = new ConnectedDeviceViewModel
                {
                    DeviceName = e.Client.DeviceName,
                    DeviceType = e.Client.DeviceType,
                    IpAddress = "Unknown", // Could extract from TcpClient
                    ConnectedAt = e.Client.ConnectedAt.ToString("HH:mm:ss"),
                    Status = "Connected"
                };

                _connectedDevices.Add(deviceViewModel);
                UpdateStatus($"Device connected: {e.Client.DeviceName}");
                NetworkStatusText.Text = $"Connected ({_connectedDevices.Count})";
                AddLog($"Device connected: {e.Client.DeviceName} ({e.Client.DeviceType})");
            });
        }

        private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var device = _connectedDevices.FirstOrDefault(d => d.DeviceName == e.Client.DeviceName);
                if (device != null)
                {
                    _connectedDevices.Remove(device);
                }

                UpdateStatus($"Device disconnected: {e.Client.DeviceName}");
                NetworkStatusText.Text = _connectedDevices.Count > 0 ? $"Connected ({_connectedDevices.Count})" : "Offline";
                AddLog($"Device disconnected: {e.Client.DeviceName}");
            });
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // Handle different message types
            switch (e.Message.Type)
            {
                case Protocol.MessageType.InputEvent:
                    ProcessInputMessage(e.Message);
                    break;
                case Protocol.MessageType.Command:
                    ProcessCommandMessage(e.Message);
                    break;
                default:
                    AddLog($"Received {e.Message.Type} message from {e.Client.DeviceName}");
                    break;
            }
        }

        private void ProcessInputMessage(StealthMessage message)
        {
            try
            {
                if (message.Data != null)
                {
                    var json = System.Text.Encoding.UTF8.GetString(message.Data);
                    var inputEvent = System.Text.Json.JsonSerializer.Deserialize<InputEventMessage>(json);
                    if (inputEvent != null)
                    {
                        _inputReceiver!.ProcessInputEvent(inputEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error processing input message: {ex.Message}");
            }
        }

        private void ProcessCommandMessage(StealthMessage message)
        {
            try
            {
                if (message.Data != null)
                {
                    var json = System.Text.Encoding.UTF8.GetString(message.Data);
                    var command = System.Text.Json.JsonSerializer.Deserialize<CommandMessage>(json);
                    if (command != null)
                    {
                        AddLog($"Received command: {command.Type}");
                        // Handle specific commands here
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error processing command message: {ex.Message}");
            }
        }

        private void OnScreenDataCaptured(object? sender, ScreenDataCapturedEventArgs e)
        {
            // Send screen data to connected clients
            Task.Run(async () =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(e.ScreenData);
                    var data = System.Text.Encoding.UTF8.GetBytes(json);

                    var message = new StealthMessage
                    {
                        Type = Protocol.MessageType.ScreenData,
                        Data = data,
                        DeviceId = _trustedCodeManager!.GetDeviceId()
                    };

                    await _connectionManager!.BroadcastMessageAsync(message);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => AddLog($"Error sending screen data: {ex.Message}"));
                }
            });
        }

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
            StatusBarText.Text = status;
            AddLog(status);
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\n";
            
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(logEntry);
                LogTextBox.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _screenStreamer?.Dispose();
            _connectionManager?.Dispose();
            _statusTimer?.Stop();
            base.OnClosed(e);
        }
    }

    public class ConnectedDeviceViewModel
    {
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string ConnectedAt { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
