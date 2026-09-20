using System.Security.Cryptography;

namespace HesabdariAsan.Native.Services;

public sealed class AdminSecurityService
{
    private readonly SettingsService _settings = new();
    private const int Iterations = 120_000;

    public bool HasPassword => !string.IsNullOrWhiteSpace(_settings.Get("admin_password_hash", "")) && !string.IsNullOrWhiteSpace(_settings.Get("admin_password_salt", ""));

    public bool Verify(string password)
    {
        if (!HasPassword) return true;
        try
        {
            var salt = Convert.FromBase64String(_settings.Get("admin_password_salt", ""));
            var expected = Convert.FromBase64String(_settings.Get("admin_password_hash", ""));
            var actual = Rfc2898DeriveBytes.Pbkdf2(password ?? "", salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    public void SetPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4) throw new InvalidOperationException("رمز مدیر حداقل 4 کاراکتر باشد.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        _settings.Set("admin_password_salt", Convert.ToBase64String(salt));
        _settings.Set("admin_password_hash", Convert.ToBase64String(hash));
    }
}
