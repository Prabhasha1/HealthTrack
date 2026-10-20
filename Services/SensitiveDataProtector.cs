using Microsoft.AspNetCore.DataProtection;

namespace HealthTrack.Services
{
    /// <summary>Encrypts and decrypts sensitive free-text health data.</summary>
    public interface ISensitiveDataProtector
    {
        string Protect(string plainText);
        string Unprotect(string cipherText);
    }

    /// <summary>
    /// Wraps ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256 by default).
    /// Keys are stored in the App_Data/keys folder so encrypted data survives restarts.
    /// </summary>
    public class SensitiveDataProtector : ISensitiveDataProtector
    {
        private const string Purpose = "HealthTrack.SensitiveHealthData.v1";
        private const string Prefix = "enc:";   // marks values that are encrypted
        private readonly IDataProtector _protector;

        public SensitiveDataProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            return Prefix + _protector.Protect(plainText);
        }

        public string Unprotect(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText) || !cipherText.StartsWith(Prefix))
                return cipherText; // value was stored before encryption was enabled
            try
            {
                return _protector.Unprotect(cipherText.Substring(Prefix.Length));
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Key lost or data tampered with: never crash the page, just hide the value.
                return "[encrypted data could not be read]";
            }
        }
    }
}
