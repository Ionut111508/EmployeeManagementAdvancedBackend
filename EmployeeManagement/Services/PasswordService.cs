using System.Security.Cryptography;

namespace EmployeeManagement.Services;

public class PasswordService : IPasswordService
{
    private const string Prefix = "PBKDF2";
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string storedPassword, string providedPassword, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrWhiteSpace(storedPassword) || string.IsNullOrEmpty(providedPassword))
            return false;

        if (!IsHashed(storedPassword))
        {
            needsRehash = true;
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedPassword),
                System.Text.Encoding.UTF8.GetBytes(providedPassword));
        }

        var parts = storedPassword.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            needsRehash = iterations < Iterations;
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool IsHashed(string storedPassword) =>
        storedPassword.StartsWith($"{Prefix}$", StringComparison.Ordinal);
}
