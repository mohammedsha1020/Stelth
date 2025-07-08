using System.Text.Json;

namespace Stealth.Shared
{
    /// <summary>
    /// Manages trusted codes for secure device authentication
    /// </summary>
    public class TrustedCodeManager
    {
        private const string TrustedCodeFileName = "trusted_codes.json";
        private const string DeviceIdFileName = "device_id.txt";
        
        private readonly string _dataDirectory;
        private TrustedCodeData? _data;

        public TrustedCodeManager(string? dataDirectory = null)
        {
            _dataDirectory = dataDirectory ?? GetDefaultDataDirectory();
            EnsureDataDirectoryExists();
            LoadTrustedCodes();
        }

        private static string GetDefaultDataDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Stealth");
        }

        private void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        private string TrustedCodeFilePath => Path.Combine(_dataDirectory, TrustedCodeFileName);
        private string DeviceIdFilePath => Path.Combine(_dataDirectory, DeviceIdFileName);

        /// <summary>
        /// Gets or generates a unique device ID
        /// </summary>
        public string GetDeviceId()
        {
            if (File.Exists(DeviceIdFilePath))
            {
                var existingId = File.ReadAllText(DeviceIdFilePath).Trim();
                if (!string.IsNullOrEmpty(existingId))
                    return existingId;
            }

            var newId = Guid.NewGuid().ToString();
            File.WriteAllText(DeviceIdFilePath, newId);
            return newId;
        }

        /// <summary>
        /// Adds a new trusted code for a device
        /// </summary>
        public void AddTrustedCode(string deviceId, string trustedCode, string deviceName = "")
        {
            _data ??= new TrustedCodeData();

            var hashedCode = Encryption.HashTrustedCode(trustedCode);
            _data.TrustedDevices[deviceId] = new TrustedDevice
            {
                DeviceId = deviceId,
                DeviceName = deviceName,
                HashedTrustedCode = hashedCode,
                DateAdded = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                IsActive = true
            };

            SaveTrustedCodes();
        }

        /// <summary>
        /// Verifies if a device and trusted code combination is valid
        /// </summary>
        public bool VerifyTrustedCode(string deviceId, string trustedCode)
        {
            if (_data?.TrustedDevices.TryGetValue(deviceId, out var device) == true)
            {
                var isValid = Encryption.VerifyTrustedCode(trustedCode, device.HashedTrustedCode);
                if (isValid)
                {
                    device.LastSeen = DateTime.UtcNow;
                    SaveTrustedCodes();
                }
                return isValid && device.IsActive;
            }
            return false;
        }

        /// <summary>
        /// Gets the current trusted code for this device (if any)
        /// </summary>
        public string? GetMyTrustedCode()
        {
            return _data?.MyTrustedCode;
        }

        /// <summary>
        /// Sets the trusted code for this device
        /// </summary>
        public void SetMyTrustedCode(string trustedCode)
        {
            _data ??= new TrustedCodeData();
            _data.MyTrustedCode = trustedCode;
            SaveTrustedCodes();
        }

        /// <summary>
        /// Generates and sets a new trusted code for this device
        /// </summary>
        public string GenerateNewTrustedCode()
        {
            var newCode = Encryption.GenerateSecureTrustedCode();
            SetMyTrustedCode(newCode);
            return newCode;
        }

        /// <summary>
        /// Removes a trusted device
        /// </summary>
        public void RemoveTrustedDevice(string deviceId)
        {
            _data?.TrustedDevices.Remove(deviceId);
            SaveTrustedCodes();
        }

        /// <summary>
        /// Gets all trusted devices
        /// </summary>
        public IEnumerable<TrustedDevice> GetTrustedDevices()
        {
            return _data?.TrustedDevices.Values ?? Enumerable.Empty<TrustedDevice>();
        }

        /// <summary>
        /// Deactivates a trusted device without removing it
        /// </summary>
        public void DeactivateDevice(string deviceId)
        {
            if (_data?.TrustedDevices.TryGetValue(deviceId, out var device) == true)
            {
                device.IsActive = false;
                SaveTrustedCodes();
            }
        }

        /// <summary>
        /// Reactivates a trusted device
        /// </summary>
        public void ReactivateDevice(string deviceId)
        {
            if (_data?.TrustedDevices.TryGetValue(deviceId, out var device) == true)
            {
                device.IsActive = true;
                SaveTrustedCodes();
            }
        }

        private void LoadTrustedCodes()
        {
            if (File.Exists(TrustedCodeFilePath))
            {
                try
                {
                    var json = File.ReadAllText(TrustedCodeFilePath);
                    _data = JsonSerializer.Deserialize<TrustedCodeData>(json);
                }
                catch
                {
                    _data = new TrustedCodeData();
                }
            }
            else
            {
                _data = new TrustedCodeData();
            }
        }

        private void SaveTrustedCodes()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TrustedCodeFilePath, json);
            }
            catch
            {
                // Silently fail - could add logging here
            }
        }
    }

    [Serializable]
    public class TrustedCodeData
    {
        public string? MyTrustedCode { get; set; }
        public Dictionary<string, TrustedDevice> TrustedDevices { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    [Serializable]
    public class TrustedDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string HashedTrustedCode { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
