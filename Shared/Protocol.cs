using Newtonsoft.Json;

namespace Stealth.Shared
{
    /// <summary>
    /// Defines the communication protocol between PC and Mobile devices
    /// </summary>
    public class Protocol
    {
        public enum MessageType
        {
            Handshake,
            ScreenData,
            InputEvent,
            Command,
            HeartBeat,
            Disconnect
        }

        public enum InputType
        {
            MouseMove,
            MouseClick,
            MouseScroll,
            KeyPress,
            TouchMove,
            TouchTap,
            TouchDrag
        }

        public enum CommandType
        {
            StartScreenShare,
            StopScreenShare,
            RequestControl,
            ReleaseControl,
            GetStatus
        }
    }

    [Serializable]
    public class StealthMessage
    {
        public Protocol.MessageType Type { get; set; }
        public string? TrustedCode { get; set; }
        public DateTime Timestamp { get; set; }
        public byte[]? Data { get; set; }
        public string? DeviceId { get; set; }

        public StealthMessage()
        {
            Timestamp = DateTime.UtcNow;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static StealthMessage? FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<StealthMessage>(json);
            }
            catch
            {
                return null;
            }
        }
    }

    [Serializable]
    public class ScreenDataMessage
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[]? ImageData { get; set; }
        public string? CompressionFormat { get; set; } // "JPEG", "PNG", "RAW"
        public DateTime CaptureTime { get; set; }

        public ScreenDataMessage()
        {
            CaptureTime = DateTime.UtcNow;
        }
    }

    [Serializable]
    public class InputEventMessage
    {
        public Protocol.InputType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Button { get; set; } // Mouse button or key code
        public float Pressure { get; set; } // For touch events
        public string? KeyChar { get; set; }
        public DateTime EventTime { get; set; }

        public InputEventMessage()
        {
            EventTime = DateTime.UtcNow;
        }
    }

    [Serializable]
    public class CommandMessage
    {
        public Protocol.CommandType Type { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public DateTime CommandTime { get; set; }

        public CommandMessage()
        {
            CommandTime = DateTime.UtcNow;
            Parameters = new Dictionary<string, object>();
        }
    }

    [Serializable]
    public class HandshakeMessage
    {
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceType { get; set; } // "PC" or "Mobile"
        public string? TrustedCode { get; set; }
        public string? Version { get; set; }
        public bool IsResponse { get; set; }
        public bool IsAccepted { get; set; }

        public HandshakeMessage()
        {
            Version = "1.0.0";
        }
    }
}
