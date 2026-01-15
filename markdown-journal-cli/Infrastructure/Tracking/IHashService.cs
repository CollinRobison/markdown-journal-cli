using System;
using System.Security.Cryptography;

namespace markdown_journal_cli.Infrastructure.Tracking;

public interface IHashService
{
    public string ComputeFileHash(string filePath);
}

public class HashService : IHashService
{
    public string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

