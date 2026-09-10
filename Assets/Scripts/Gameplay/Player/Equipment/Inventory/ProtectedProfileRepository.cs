using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public enum ProfileReadResult { Missing, Loaded, Recovered, Invalid }

    public interface IProfileRepository
    {
        ProfileReadResult Read(Func<string, bool> validate, out string payload);
        void Write(string payload);
    }

    /// <summary>AES-256-CBC + encrypt-then-HMAC-SHA256. Local tamper deterrence, not server authority.</summary>
    public sealed class ProtectedProfileRepository : IProfileRepository
    {
        const int MaximumBytes = 1024 * 1024;
        static readonly byte[] Header = { 77, 73, 83, 77, 79, 0, 1, 0 };
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        readonly string path;
        readonly byte[] encryptionKey = new byte[32], authenticationKey = new byte[32];
        bool primaryValidated;
        bool writable;

        public ProtectedProfileRepository(string filePath)
        {
            path = Path.GetFullPath(filePath);
            // A recoverable application key keeps saves portable. A determined local attacker
            // can extract it or patch the process; this does not claim anti-cheat guarantees.
            using (var sha = SHA512.Create())
            {
                var material = sha.ComputeHash(Encoding.UTF8.GetBytes("Mismo/offline-profile/v1/71c4a38e-3d68-4d03-850b-68c40b1de9af"));
                Buffer.BlockCopy(material, 0, encryptionKey, 0, 32);
                Buffer.BlockCopy(material, 32, authenticationKey, 0, 32);
            }
        }

        public ProfileReadResult Read(Func<string, bool> validate, out string payload)
        {
            primaryValidated = writable = false;
            if (TryRead(path, validate, out payload))
            { primaryValidated = writable = true; return ProfileReadResult.Loaded; }
            if (TryRead(path + ".bak", validate, out payload))
            { writable = true; return ProfileReadResult.Recovered; }
            if (File.Exists(path) || File.Exists(path + ".bak")) return ProfileReadResult.Invalid;
            writable = true;
            return ProfileReadResult.Missing;
        }

        bool TryRead(string candidate, Func<string, bool> validate, out string payload)
        {
            payload = null;
            if (!File.Exists(candidate)) return false;
            try
            {
                if (new FileInfo(candidate).Length > MaximumBytes) return false;
                var decoded = Decode(File.ReadAllBytes(candidate));
                if (!validate(decoded)) return false;
                payload = decoded;
                return true;
            }
            catch (Exception e) when (e is CryptographicException || e is InvalidDataException ||
                e is DecoderFallbackException || e is ArgumentException) { return false; }
        }

        public void Write(string payload)
        {
            if (!writable) throw new InvalidOperationException("Read and validate the profile before writing.");
            var bytes = Encode(payload);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, primaryValidated ? path + ".bak" : null);
                else File.Move(temporary, path);
                primaryValidated = true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        byte[] Encode(string payload)
        {
            byte[] plain = Utf8.GetBytes(payload);
            if (plain.Length > MaximumBytes - 128) throw new InvalidDataException("Profile is too large.");
            using (var aes = Aes.Create())
            {
                aes.Key = encryptionKey; aes.GenerateIV(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var encryptor = aes.CreateEncryptor())
                {
                    var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
                    var result = new byte[Header.Length + 16 + cipher.Length + 32];
                    Buffer.BlockCopy(Header, 0, result, 0, Header.Length);
                    Buffer.BlockCopy(aes.IV, 0, result, Header.Length, 16);
                    Buffer.BlockCopy(cipher, 0, result, Header.Length + 16, cipher.Length);
                    using (var hmac = new HMACSHA256(authenticationKey))
                    {
                        var tag = hmac.ComputeHash(result, 0, result.Length - 32);
                        Buffer.BlockCopy(tag, 0, result, result.Length - 32, 32);
                    }
                    return result;
                }
            }
        }

        string Decode(byte[] bytes)
        {
            if (bytes.Length < 72 || bytes.Length > MaximumBytes || (bytes.Length - 56) % 16 != 0)
                throw new InvalidDataException("Invalid profile envelope.");
            using (var hmac = new HMACSHA256(authenticationKey))
            {
                var expected = hmac.ComputeHash(bytes, 0, bytes.Length - 32);
                int difference = 0;
                for (int i = 0; i < 32; i++) difference |= expected[i] ^ bytes[bytes.Length - 32 + i];
                for (int i = 0; i < Header.Length; i++) difference |= Header[i] ^ bytes[i];
                if (difference != 0) throw new CryptographicException("Profile authentication failed.");
            }
            using (var aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                var iv = new byte[16]; Buffer.BlockCopy(bytes, Header.Length, iv, 0, 16); aes.IV = iv;
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                    return Utf8.GetString(decryptor.TransformFinalBlock(bytes, 24, bytes.Length - 56));
            }
        }
    }
}
