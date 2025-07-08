using System.Net;
using System.Net.Sockets;
using Stealth.Shared;

namespace Stealth.PC
{
    /// <summary>
    /// Manages network connections between PC and mobile devices
    /// </summary>
    public class ConnectionManager : IDisposable
    {
        private const int DefaultPort = 7890;
        private readonly TrustedCodeManager _trustedCodeManager;
        private readonly string _deviceId;
        private TcpListener? _server;
        private readonly List<ConnectedClient> _clients;
        private bool _isRunning;
        private byte[]? _encryptionKey;

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public ConnectionManager()
        {
            _trustedCodeManager = new TrustedCodeManager();
            _deviceId = _trustedCodeManager.GetDeviceId();
            _clients = new List<ConnectedClient>();
        }

        /// <summary>
        /// Starts the TCP server to listen for incoming connections
        /// </summary>
        public async Task StartServerAsync(int port = DefaultPort)
        {
            if (_isRunning) return;

            _server = new TcpListener(IPAddress.Any, port);
            _server.Start();
            _isRunning = true;

            Console.WriteLine($"Stealth PC Server started on port {port}");
            Console.WriteLine($"Device ID: {_deviceId}");

            // Start accepting clients
            _ = Task.Run(AcceptClientsAsync);
        }

        /// <summary>
        /// Stops the server and disconnects all clients
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _server?.Stop();

            lock (_clients)
            {
                foreach (var client in _clients.ToList())
                {
                    client.Disconnect();
                }
                _clients.Clear();
            }

            Console.WriteLine("Stealth PC Server stopped");
        }

        /// <summary>
        /// Connects to a mobile device as a client
        /// </summary>
        public async Task<bool> ConnectToDeviceAsync(string ipAddress, int port = DefaultPort)
        {
            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(ipAddress, port);

                var client = new ConnectedClient(tcpClient, _deviceId, "PC");
                
                // Perform handshake
                var myTrustedCode = _trustedCodeManager.GetMyTrustedCode();
                if (string.IsNullOrEmpty(myTrustedCode))
                {
                    myTrustedCode = _trustedCodeManager.GenerateNewTrustedCode();
                    Console.WriteLine($"Generated new trusted code: {myTrustedCode}");
                }

                if (await PerformHandshakeAsync(client, myTrustedCode))
                {
                    lock (_clients)
                    {
                        _clients.Add(client);
                    }

                    // Setup encryption key
                    _encryptionKey = Encryption.GenerateKeyFromTrustedCode(myTrustedCode);

                    // Start listening for messages
                    _ = Task.Run(() => ListenToClientAsync(client));

                    ClientConnected?.Invoke(this, new ClientConnectedEventArgs { Client = client });
                    return true;
                }
                else
                {
                    client.Disconnect();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to device: {ex.Message}");
                return false;
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning && _server != null)
            {
                try
                {
                    var tcpClient = await _server.AcceptTcpClientAsync();
                    var client = new ConnectedClient(tcpClient, _deviceId, "PC");
                    
                    Console.WriteLine($"Client connected from {tcpClient.Client.RemoteEndPoint}");

                    // Handle the client in a separate task
                    _ = Task.Run(() => HandleNewClientAsync(client));
                }
                catch when (!_isRunning)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleNewClientAsync(ConnectedClient client)
        {
            try
            {
                // Wait for handshake from client
                if (await WaitForHandshakeAsync(client))
                {
                    lock (_clients)
                    {
                        _clients.Add(client);
                    }

                    ClientConnected?.Invoke(this, new ClientConnectedEventArgs { Client = client });

                    // Start listening for messages
                    await ListenToClientAsync(client);
                }
                else
                {
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                client.Disconnect();
            }
        }

        private async Task<bool> WaitForHandshakeAsync(ConnectedClient client)
        {
            // Implementation for receiving and verifying handshake
            try
            {
                var message = await client.ReceiveMessageAsync();
                if (message?.Type == Protocol.MessageType.Handshake)
                {
                    var handshake = System.Text.Json.JsonSerializer.Deserialize<HandshakeMessage>(
                        System.Text.Encoding.UTF8.GetString(message.Data ?? Array.Empty<byte>()));

                    if (handshake != null && !string.IsNullOrEmpty(handshake.TrustedCode))
                    {
                        var isValid = _trustedCodeManager.VerifyTrustedCode(handshake.DeviceId ?? "", handshake.TrustedCode);
                        
                        if (!isValid && !string.IsNullOrEmpty(handshake.DeviceId))
                        {
                            // First time connection - add the device
                            _trustedCodeManager.AddTrustedCode(handshake.DeviceId, handshake.TrustedCode, handshake.DeviceName ?? "Unknown Device");
                            isValid = true;
                            Console.WriteLine($"Added new trusted device: {handshake.DeviceName} ({handshake.DeviceId})");
                        }

                        // Send response
                        var response = new HandshakeMessage
                        {
                            DeviceId = _deviceId,
                            DeviceName = Environment.MachineName,
                            DeviceType = "PC",
                            TrustedCode = _trustedCodeManager.GetMyTrustedCode(),
                            IsResponse = true,
                            IsAccepted = isValid
                        };

                        await client.SendHandshakeAsync(response);

                        if (isValid)
                        {
                            client.DeviceId = handshake.DeviceId ?? "";
                            client.DeviceName = handshake.DeviceName ?? "Unknown Device";
                            _encryptionKey = Encryption.GenerateKeyFromTrustedCode(handshake.TrustedCode);
                        }

                        return isValid;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> PerformHandshakeAsync(ConnectedClient client, string trustedCode)
        {
            try
            {
                var handshake = new HandshakeMessage
                {
                    DeviceId = _deviceId,
                    DeviceName = Environment.MachineName,
                    DeviceType = "PC",
                    TrustedCode = trustedCode,
                    IsResponse = false
                };

                await client.SendHandshakeAsync(handshake);

                // Wait for response
                var response = await client.ReceiveMessageAsync();
                if (response?.Type == Protocol.MessageType.Handshake)
                {
                    var handshakeResponse = System.Text.Json.JsonSerializer.Deserialize<HandshakeMessage>(
                        System.Text.Encoding.UTF8.GetString(response.Data ?? Array.Empty<byte>()));

                    return handshakeResponse?.IsAccepted == true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task ListenToClientAsync(ConnectedClient client)
        {
            try
            {
                while (client.IsConnected)
                {
                    var message = await client.ReceiveMessageAsync();
                    if (message != null)
                    {
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs 
                        { 
                            Client = client, 
                            Message = message 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listening to client: {ex.Message}");
            }
            finally
            {
                RemoveClient(client);
            }
        }

        private void RemoveClient(ConnectedClient client)
        {
            lock (_clients)
            {
                _clients.Remove(client);
            }

            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { Client = client });
            client.Disconnect();
        }

        /// <summary>
        /// Sends a message to all connected clients
        /// </summary>
        public async Task BroadcastMessageAsync(StealthMessage message)
        {
            var clients = _clients.ToList();
            foreach (var client in clients)
            {
                try
                {
                    await client.SendMessageAsync(message);
                }
                catch
                {
                    RemoveClient(client);
                }
            }
        }

        /// <summary>
        /// Gets all currently connected clients
        /// </summary>
        public IReadOnlyList<ConnectedClient> GetConnectedClients()
        {
            lock (_clients)
            {
                return _clients.ToList();
            }
        }

        public void Dispose()
        {
            Stop();
            _server?.Stop();
        }
    }

    public class ConnectedClient
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;

        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; }
        public bool IsConnected => _tcpClient.Connected;

        public ConnectedClient(TcpClient tcpClient, string deviceId, string deviceType)
        {
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            DeviceId = deviceId;
            DeviceType = deviceType;
            ConnectedAt = DateTime.UtcNow;
        }

        public async Task SendMessageAsync(StealthMessage message)
        {
            var json = message.ToJson();
            var data = System.Text.Encoding.UTF8.GetBytes(json);
            var length = BitConverter.GetBytes(data.Length);
            
            await _stream.WriteAsync(length, 0, 4);
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }

        public async Task SendHandshakeAsync(HandshakeMessage handshake)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(handshake);
            var data = System.Text.Encoding.UTF8.GetBytes(json);
            
            var message = new StealthMessage
            {
                Type = Protocol.MessageType.Handshake,
                Data = data,
                DeviceId = DeviceId
            };

            await SendMessageAsync(message);
        }

        public async Task<StealthMessage?> ReceiveMessageAsync()
        {
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

                var json = System.Text.Encoding.UTF8.GetString(buffer);
                return StealthMessage.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        public void Disconnect()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch
            {
                // Ignore errors during disconnect
            }
        }
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        public ConnectedClient Client { get; set; } = null!;
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public ConnectedClient Client { get; set; } = null!;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public ConnectedClient Client { get; set; } = null!;
        public StealthMessage Message { get; set; } = null!;
    }
}
