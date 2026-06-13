using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TBM.Application.Interfaces;
using TBM.Application.Interfaces.Security;

namespace TBM.Application.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        var secret = configuration["Security:EncryptionKey"];

        if (string.IsNullOrWhiteSpace(secret))
            throw new Exception("Encryption key not configured.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var cipherBytes = encryptor.TransformFinalBlock(
            plainBytes, 0, plainBytes.Length);

        var result = aes.IV.Concat(cipherBytes).ToArray();
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = fullCipher.Take(16).ToArray();
        var cipher = fullCipher.Skip(16).ToArray();

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();

        var plainBytes = decryptor.TransformFinalBlock(
            cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
