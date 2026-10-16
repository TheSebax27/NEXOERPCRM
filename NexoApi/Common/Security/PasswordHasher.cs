using System.Security.Cryptography;

namespace NexoApi.Common.Security;

public static class PasswordHasher
{
    private const int SaltSize = 128 / 8;
    private const int HashSize = 256 / 8;
    private const int Iterations = 100_000;

    public static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (hash, salt);
    }

    public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(password, storedSalt, Iterations, HashAlgorithmName.SHA256, storedHash.Length);
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}