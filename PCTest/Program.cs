using Stealth.Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PCTest;

class Program
{
    private static TrustedCodeManager? _trustedCodeManager;
    private static PCConnectionManager? _connectionManager;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Stealth PC Test Server ===");
        
        // Initialize components
        _trustedCodeManager = new TrustedCodeManager();
        _connectionManager = new PCConnectionManager();
        
        var deviceId = _trustedCodeManager.GetDeviceId();
        var trustedCode = _trustedCodeManager.GetMyTrustedCode();
        
        if (string.IsNullOrEmpty(trustedCode))
        {
            trustedCode = Encryption.GenerateSecureTrustedCode();
            _trustedCodeManager.SetMyTrustedCode(trustedCode);
            Console.WriteLine($"Generated new trusted code: {trustedCode}");
        }
        
        Console.WriteLine($"PC Device ID: {deviceId}");
        Console.WriteLine($"Trusted Code: {trustedCode}");
        Console.WriteLine();
        
        // Setup event handlers
        _connectionManager.ClientConnected += OnClientConnected;
        _connectionManager.MessageReceived += OnMessageReceived;
        
        // Start server
        Console.WriteLine("Starting server on port 7890...");
        await _connectionManager.StartServerAsync(7890);
        
        Console.WriteLine("Server started! Waiting for mobile connections...");
        Console.WriteLine("Press 'q' to quit");
        
        // Keep running until user quits
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                break;
        }
        
        Console.WriteLine("Shutting down...");
        _connectionManager.Stop();
    }
    
    private static void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        Console.WriteLine($"Mobile client connected: {e.Client.DeviceName} ({e.Client.DeviceId})");
        
        // Start sending mock screen data for testing
        Task.Run(() => SendMockScreenData(e.Client));
    }
    
    private static void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        Console.WriteLine($"Received {e.Message.Type} from {e.Client.DeviceName}");
        
        if (e.Message.Type == Protocol.MessageType.Command)
        {
            var json = Encoding.UTF8.GetString(e.Message.Data ?? Array.Empty<byte>());
            var command = System.Text.Json.JsonSerializer.Deserialize<CommandMessage>(json);
            
            if (command?.Type == Protocol.CommandType.StartScreenShare)
            {
                Console.WriteLine("Mobile requested screen sharing to start");
            }
            else if (command?.Type == Protocol.CommandType.StopScreenShare)
            {
                Console.WriteLine("Mobile requested screen sharing to stop");
            }
        }
        else if (e.Message.Type == Protocol.MessageType.InputEvent)
        {
            var json = Encoding.UTF8.GetString(e.Message.Data ?? Array.Empty<byte>());
            var inputEvent = System.Text.Json.JsonSerializer.Deserialize<InputEventMessage>(json);
            
            Console.WriteLine($"Received input: {inputEvent?.Type} at ({inputEvent?.X}, {inputEvent?.Y})");
        }
    }
    
    private static async Task SendMockScreenData(PCClient client)
    {
        await Task.Delay(2000); // Wait a bit before starting
        
        for (int i = 0; i < 10; i++)
        {
            try
            {
                // Create mock screen data
                var mockImageData = GenerateMockImage(800, 600);
                
                var screenData = new ScreenDataMessage
                {
                    Width = 800,
                    Height = 600,
                    ImageData = mockImageData,
                    CompressionFormat = "MOCK"
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(screenData);
                var data = Encoding.UTF8.GetBytes(json);
                
                var message = new StealthMessage
                {
                    Type = Protocol.MessageType.ScreenData,
                    DeviceId = _trustedCodeManager!.GetDeviceId(),
                    Data = data
                };
                
                await client.SendMessageAsync(message);
                Console.WriteLine($"Sent mock screen data #{i + 1} to mobile");
                
                await Task.Delay(1000); // Send every second
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending mock data: {ex.Message}");
                break;
            }
        }
        
        Console.WriteLine("Finished sending mock screen data");
    }
    
    private static byte[] GenerateMockImage(int width, int height)
    {
        // Generate a simple pattern as mock image data
        var data = new byte[width * height * 3]; // RGB
        var random = new Random();
        
        for (int i = 0; i < data.Length; i += 3)
        {
            data[i] = (byte)random.Next(256);     // Red
            data[i + 1] = (byte)random.Next(256); // Green  
            data[i + 2] = (byte)random.Next(256); // Blue
        }
        
        return data;
    }
}

// Simplified PC Connection Manager for testing
public class PCConnectionManager
{
    private TcpListener? _server;
    private readonly List<PCClient> _clients = new();
    private bool _isRunning;
    private readonly TrustedCodeManager _trustedCodeManager;

    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public PCConnectionManager()
    {
        _trustedCodeManager = new TrustedCodeManager();
    }

    public async Task StartServerAsync(int port)
    {
        _server = new TcpListener(IPAddress.Any, port);
        _server.Start();
        _isRunning = true;

        _ = Task.Run(AcceptClientsAsync);
    }

    public void Stop()
    {
        _isRunning = false;
        _server?.Stop();
        
        foreach (var client in _clients.ToList())
        {
            client.Disconnect();
        }
        _clients.Clear();
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning && _server != null)
        {
            try
            {
                var tcpClient = await _server.AcceptTcpClientAsync();
                var client = new PCClient(tcpClient);
                
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch when (!_isRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(PCClient client)
    {
        try
        {
            // Wait for handshake
            var message = await client.ReceiveMessageAsync();
            if (message?.Type == Protocol.MessageType.Handshake)
            {
                var handshake = System.Text.Json.JsonSerializer.Deserialize<HandshakeMessage>(
                    Encoding.UTF8.GetString(message.Data ?? Array.Empty<byte>()));

                if (handshake != null)
                {
                    client.DeviceId = handshake.DeviceId ?? "";
                    client.DeviceName = handshake.DeviceName ?? "Unknown";
                    
                    // Verify or add trusted code
                    var isValid = _trustedCodeManager.VerifyTrustedCode(client.DeviceId, handshake.TrustedCode ?? "");
                    if (!isValid && !string.IsNullOrEmpty(handshake.TrustedCode))
                    {
                        _trustedCodeManager.AddTrustedCode(client.DeviceId, handshake.TrustedCode, client.DeviceName);
                        isValid = true;
                    }

                    // Send response
                    var response = new HandshakeMessage
                    {
                        DeviceId = _trustedCodeManager.GetDeviceId(),
                        DeviceName = "PC Test Server",
                        DeviceType = "PC",
                        IsResponse = true,
                        IsAccepted = isValid
                    };

                    var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
                    var responseMessage = new StealthMessage
                    {
                        Type = Protocol.MessageType.Handshake,
                        DeviceId = _trustedCodeManager.GetDeviceId(),
                        Data = Encoding.UTF8.GetBytes(responseJson)
                    };

                    await client.SendMessageAsync(responseMessage);

                    if (isValid)
                    {
                        _clients.Add(client);
                        ClientConnected?.Invoke(this, new ClientConnectedEventArgs { Client = client });
                        
                        // Start listening for messages
                        await ListenToClientAsync(client);
                    }
                    else
                    {
                        client.Disconnect();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
            client.Disconnect();
        }
    }

    private async Task ListenToClientAsync(PCClient client)
    {
        try
        {
            while (client.IsConnected)
            {
                var message = await client.ReceiveMessageAsync();
                if (message != null)
                {
                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Client = client, Message = message });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listening to client: {ex.Message}");
        }
        finally
        {
            _clients.Remove(client);
            client.Disconnect();
        }
    }
}

public class PCClient
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;

    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public bool IsConnected => _tcpClient.Connected;

    public PCClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
    }

    public async Task SendMessageAsync(StealthMessage message)
    {
        var json = message.ToJson();
        var data = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(data.Length);

        await _stream.WriteAsync(length, 0, 4);
        await _stream.WriteAsync(data, 0, data.Length);
        await _stream.FlushAsync();
    }

    public async Task<StealthMessage?> ReceiveMessageAsync()
    {
        try
        {
            var lengthBuffer = new byte[4];
            var bytesRead = await _stream.ReadAsync(lengthBuffer, 0, 4);
            if (bytesRead != 4) return null;

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 10 * 1024 * 1024) return null;

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

    public void Disconnect()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }
    }
}

public class ClientConnectedEventArgs : EventArgs
{
    public PCClient Client { get; set; } = null!;
}

public class MessageReceivedEventArgs : EventArgs
{
    public PCClient Client { get; set; } = null!;
    public StealthMessage Message { get; set; } = null!;
}
