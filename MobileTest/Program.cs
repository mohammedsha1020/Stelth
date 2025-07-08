using Stealth.Shared;
using System.Net;

namespace MobileTest;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing Mobile Connection Manager...");
        
        // Test encryption and trusted code functionality
        var trustedCodeManager = new TrustedCodeManager();
        var deviceId = trustedCodeManager.GetDeviceId();
        var trustedCode = Encryption.GenerateSecureTrustedCode();
        
        Console.WriteLine($"Device ID: {deviceId}");
        Console.WriteLine($"Trusted Code: {trustedCode}");
        
        // Test encryption
        var testData = "Hello, World!";
        var key = Encryption.GenerateKeyFromTrustedCode(trustedCode);
        var encrypted = Encryption.EncryptString(testData, key);
        var decrypted = Encryption.DecryptString(encrypted, key);
        
        Console.WriteLine($"Original: {testData}");
        Console.WriteLine($"Encrypted length: {encrypted.Length}");
        Console.WriteLine($"Decrypted: {decrypted}");
        Console.WriteLine($"Encryption test: {(testData == decrypted ? "PASSED" : "FAILED")}");
        
        // Test protocol message serialization
        var handshakeMessage = new HandshakeMessage
        {
            DeviceId = deviceId,
            TrustedCode = trustedCode,
            DeviceType = "Mobile"
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(handshakeMessage);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<HandshakeMessage>(json);
        
        Console.WriteLine($"Handshake serialization test: {(handshakeMessage.DeviceId == deserialized?.DeviceId ? "PASSED" : "FAILED")}");
        
        // Test trusted code verification
        trustedCodeManager.AddTrustedCode(deviceId, trustedCode, "Test Device");
        var isValid = trustedCodeManager.VerifyTrustedCode(deviceId, trustedCode);
        Console.WriteLine($"Trusted code verification test: {(isValid ? "PASSED" : "FAILED")}");
        
        Console.WriteLine("Mobile core functionality tests completed successfully!");
    }
}
