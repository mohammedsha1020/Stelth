#nullable enable
using System.Net.Sockets;
using System.Text;
using Stealth.Shared;

namespace Stealth.Mobile
{
    /// <summary>
    /// Manages network connection to PC for the mobile application
    /// </summary>
    public class MobileConnectionManager : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly TrustedCodeManager _trustedCodeManager;
        private bool _isConnected;
        private bool _isListening;
        private Task? _listenTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private byte[]? _encryptionKey;

        public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;
        public event EventHandler<ScreenDataReceivedEventArgs>? ScreenDataReceived;
        public event EventHandler<string>? LogMessageReceived;

        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
        public string DeviceId { get; }

        public MobileConnectionManager()
        {
            _trustedCodeManager = new TrustedCodeManager();
            DeviceId = _trustedCodeManager.GetDeviceId();
        }

        /// <summary>
        /// Connects to the PC server
        /// </summary>
        public async Task<bool> ConnectAsync(string serverIp, int port, string trustedCode)
        {
            try
            {
                if (_isConnected)
                {
                    await DisconnectAsync();
                }

                LogMessageReceived?.Invoke(this, $"Connecting to {serverIp}:{port}...");
                
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(serverIp, port);
                _stream = _tcpClient.GetStream();

                // Save trusted code
                _trustedCodeManager.SetMyTrustedCode(trustedCode);
                _encryptionKey = Encryption.GenerateKeyFromTrustedCode(trustedCode);

                // Perform handshake
                var handshakeSuccess = await PerformHandshakeAsync(trustedCode);
                if (handshakeSuccess)
                {
                    _isConnected = true;
                    StartListening();
                    
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs 
                    { 
                        IsConnected = true, 
                        ServerAddress = serverIp,
                        Port = port
                    });
                    
                    LogMessageReceived?.Invoke(this, $"Successfully connected to {serverIp}:{port}");
                    return true;
                }
                else
                {
                    LogMessageReceived?.Invoke(this, "Handshake failed - connection rejected");
                    await DisconnectAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Connection failed: {ex.Message}");
                await DisconnectAsync();
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the PC server
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                _isListening = false;
                
                _cancellationTokenSource?.Cancel();
                
                if (_listenTask != null)
                {
                    await _listenTask;
                }

                _stream?.Close();
                _tcpClient?.Close();
                
                _stream?.Dispose();
                _tcpClient?.Dispose();
                
                _stream = null;
                _tcpClient = null;

                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs 
                { 
                    IsConnected = false 
                });
                
                LogMessageReceived?.Invoke(this, "Disconnected from PC");
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Error during disconnect: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an input event to the PC
        /// </summary>
        public async Task SendInputEventAsync(InputEventMessage inputEvent)
        {
            if (!IsConnected || _stream == null) return;

            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(inputEvent);
                var data = Encoding.UTF8.GetBytes(json);

                var message = new StealthMessage
                {
                    Type = Protocol.MessageType.InputEvent,
                    DeviceId = DeviceId,
                    Data = data
                };

                await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Failed to send input event: {ex.Message}");
            }
        }

        /// <summary>
        /// Requests the PC to start screen sharing
        /// </summary>
        public async Task RequestScreenSharingAsync(bool start)
        {
            if (!IsConnected) return;

            try
            {
                var command = new CommandMessage
                {
                    Type = start ? Protocol.CommandType.StartScreenShare : Protocol.CommandType.StopScreenShare
                };

                var json = System.Text.Json.JsonSerializer.Serialize(command);
                var data = Encoding.UTF8.GetBytes(json);

                var message = new StealthMessage
                {
                    Type = Protocol.MessageType.Command,
                    DeviceId = DeviceId,
                    Data = data
                };

                await SendMessageAsync(message);
                LogMessageReceived?.Invoke(this, $"Requested screen sharing: {(start ? "start" : "stop")}");
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Failed to request screen sharing: {ex.Message}");
            }
        }

        private async Task<bool> PerformHandshakeAsync(string trustedCode)
        {
            try
            {
                var handshake = new HandshakeMessage
                {
                    DeviceId = DeviceId,
                    DeviceName = GetDeviceName(),
                    DeviceType = "Mobile",
                    TrustedCode = trustedCode,
                    IsResponse = false
                };

                var json = System.Text.Json.JsonSerializer.Serialize(handshake);
                var data = Encoding.UTF8.GetBytes(json);

                var message = new StealthMessage
                {
                    Type = Protocol.MessageType.Handshake,
                    DeviceId = DeviceId,
                    Data = data,
                    TrustedCode = trustedCode
                };

                await SendMessageAsync(message);

                // Wait for response
                var response = await ReceiveMessageAsync();
                if (response?.Type == Protocol.MessageType.Handshake && response.Data != null)
                {
                    var handshakeResponse = System.Text.Json.JsonSerializer.Deserialize<HandshakeMessage>(
                        Encoding.UTF8.GetString(response.Data));

                    return handshakeResponse?.IsAccepted == true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Handshake error: {ex.Message}");
                return false;
            }
        }

        private void StartListening()
        {
            _isListening = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenForMessagesAsync(_cancellationTokenSource.Token));
        }

        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_isListening && !cancellationToken.IsCancellationRequested)
                {
                    var message = await ReceiveMessageAsync();
                    if (message != null)
                    {
                        await ProcessMessageAsync(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    LogMessageReceived?.Invoke(this, $"Listen error: {ex.Message}");
                    await DisconnectAsync();
                }
            }
        }

        private async Task ProcessMessageAsync(StealthMessage message)
        {
            try
            {
                switch (message.Type)
                {
                    case Protocol.MessageType.ScreenData:
                        await ProcessScreenDataAsync(message);
                        break;
                    case Protocol.MessageType.HeartBeat:
                        // Respond to heartbeat
                        var heartbeatResponse = new StealthMessage
                        {
                            Type = Protocol.MessageType.HeartBeat,
                            DeviceId = DeviceId
                        };
                        await SendMessageAsync(heartbeatResponse);
                        break;
                    default:
                        LogMessageReceived?.Invoke(this, $"Received {message.Type} message");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Error processing message: {ex.Message}");
            }
        }

        private Task ProcessScreenDataAsync(StealthMessage message)
        {
            try
            {
                if (message.Data != null)
                {
                    var json = Encoding.UTF8.GetString(message.Data);
                    var screenData = System.Text.Json.JsonSerializer.Deserialize<ScreenDataMessage>(json);
                    
                    if (screenData != null)
                    {
                        ScreenDataReceived?.Invoke(this, new ScreenDataReceivedEventArgs { ScreenData = screenData });
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Error processing screen data: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private async Task SendMessageAsync(StealthMessage message)
        {
            if (_stream == null) return;

            var json = message.ToJson();
            var data = Encoding.UTF8.GetBytes(json);
            var length = BitConverter.GetBytes(data.Length);

            await _stream.WriteAsync(length, 0, 4);
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }

        private async Task<StealthMessage?> ReceiveMessageAsync()
        {
            if (_stream == null) return null;

            try
            {
                var lengthBuffer = new byte[4];
                var bytesRead = await _stream.ReadAsync(lengthBuffer, 0, 4);
                if (bytesRead != 4) return null;

                var length = BitConverter.ToInt32(lengthBuffer, 0);
                if (length <= 0 || length > 10 * 1024 * 1024) return null; // Max 10MB

                var buffer = new byte[length];
                var totalRead = 0;
                while (totalRead < length)
                {
                    var read = await _stream.ReadAsync(buffer, totalRead, length - totalRead);
                    if (read == 0) return null;
                    totalRead += read;
                }

                var json = Encoding.UTF8.GetString(buffer);
                return StealthMessage.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDeviceName()
        {
            try
            {
                return "Mobile Device"; // Will be enhanced with platform-specific code later
            }
            catch
            {
                return "Mobile Device";
            }
        }

        public void Dispose()
        {
            Task.Run(async () => await DisconnectAsync()).Wait(5000);
            _cancellationTokenSource?.Dispose();
        }
    }

    public class ConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? ServerAddress { get; set; }
        public int Port { get; set; }
    }

    public class ScreenDataReceivedEventArgs : EventArgs
    {
        public ScreenDataMessage ScreenData { get; set; } = null!;
    }
}
