using System.Security.Cryptography;
using System.Text;

namespace Stealth.Shared
{
    /// <summary>
    /// Handles AES-256 encryption and decryption for secure communication
    /// </summary>
    public static class Encryption
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int IvSize = 16; // 128 bits / 8

        /// <summary>
        /// Generates a random AES key from a trusted code
        /// </summary>
        public static byte[] GenerateKeyFromTrustedCode(string trustedCode)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(trustedCode + "StealthSalt2025"));
        }

        /// <summary>
        /// Encrypts data using AES-256-CBC
        /// </summary>
        public static byte[] Encrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            
            // Write IV first
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }
            
            return ms.ToArray();
        }

        /// <summary>
        /// Decrypts data using AES-256-CBC
        /// </summary>
        public static byte[] Decrypt(byte[] encryptedData, byte[] key)
        {
            if (encryptedData.Length < IvSize)
                throw new ArgumentException("Encrypted data too short");

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;

            // Extract IV from the beginning
            var iv = new byte[IvSize];
            Array.Copy(encryptedData, 0, iv, 0, IvSize);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData, IvSize, encryptedData.Length - IvSize);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            
            cs.CopyTo(result);
            return result.ToArray();
        }

        /// <summary>
        /// Encrypts a string message
        /// </summary>
        public static byte[] EncryptString(string message, byte[] key)
        {
            var data = Encoding.UTF8.GetBytes(message);
            return Encrypt(data, key);
        }

        /// <summary>
        /// Decrypts to a string message
        /// </summary>
        public static string DecryptString(byte[] encryptedData, byte[] key)
        {
            var data = Decrypt(encryptedData, key);
            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Generates a secure random trusted code
        /// </summary>
        public static string GenerateSecureTrustedCode(int length = 32)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            using var rng = RandomNumberGenerator.Create();
            var result = new char[length];
            var bytes = new byte[length];
            
            rng.GetBytes(bytes);
            
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }
            
            return new string(result);
        }

        /// <summary>
        /// Creates a hash of the trusted code for secure storage
        /// </summary>
        public static string HashTrustedCode(string trustedCode)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(trustedCode + "StealthHashSalt2025"));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verifies a trusted code against its hash
        /// </summary>
        public static bool VerifyTrustedCode(string trustedCode, string hash)
        {
            return HashTrustedCode(trustedCode) == hash;
        }
    }
}
