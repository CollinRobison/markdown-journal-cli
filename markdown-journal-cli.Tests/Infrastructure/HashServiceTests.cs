using markdown_journal_cli.Infrastructure.Tracking;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="HashService"/> class, covering file hash computation.
/// </summary>
public class HashServiceTests : IDisposable
{
    private readonly HashService _hashService;
    private readonly string _tempDirectory;

    public HashServiceTests()
    {
        _hashService = new HashService();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"HashServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ComputeFileHash_Should_Return_Lowercase_Hex_String()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "test.md");
        File.WriteAllText(testFile, "Hello, World!");

        // When
        var hash = _hashService.ComputeFileHash(testFile);

        // Then
        hash.ShouldNotBeNullOrEmpty();
        hash.ShouldMatch(@"^[0-9a-f]+$"); // Only lowercase hex characters
        hash.Length.ShouldBe(64); // SHA256 produces 32 bytes = 64 hex characters
    }

    [Fact]
    public void ComputeFileHash_Should_Return_Same_Hash_For_Identical_Content()
    {
        // Given
        var testFile1 = Path.Combine(_tempDirectory, "test1.md");
        var testFile2 = Path.Combine(_tempDirectory, "test2.md");
        var content = "This is test content";
        File.WriteAllText(testFile1, content);
        File.WriteAllText(testFile2, content);

        // When
        var hash1 = _hashService.ComputeFileHash(testFile1);
        var hash2 = _hashService.ComputeFileHash(testFile2);

        // Then
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputeFileHash_Should_Return_Different_Hash_For_Different_Content()
    {
        // Given
        var testFile1 = Path.Combine(_tempDirectory, "test1.md");
        var testFile2 = Path.Combine(_tempDirectory, "test2.md");
        File.WriteAllText(testFile1, "Content A");
        File.WriteAllText(testFile2, "Content B");

        // When
        var hash1 = _hashService.ComputeFileHash(testFile1);
        var hash2 = _hashService.ComputeFileHash(testFile2);

        // Then
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeFileHash_Should_Return_Different_Hash_When_File_Changes()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "test.md");
        File.WriteAllText(testFile, "Original content");
        var originalHash = _hashService.ComputeFileHash(testFile);

        // When
        File.WriteAllText(testFile, "Modified content");
        var modifiedHash = _hashService.ComputeFileHash(testFile);

        // Then
        modifiedHash.ShouldNotBe(originalHash);
    }

    [Fact]
    public void ComputeFileHash_Should_Handle_Empty_File()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "empty.md");
        File.WriteAllText(testFile, string.Empty);

        // When
        var hash = _hashService.ComputeFileHash(testFile);

        // Then
        hash.ShouldNotBeNullOrEmpty();
        hash.Length.ShouldBe(64);
        // SHA256 hash of empty string
        hash.ShouldBe("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void ComputeFileHash_Should_Handle_Large_File()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "large.md");
        var largeContent = new string('a', 1024 * 1024); // 1MB of 'a' characters
        File.WriteAllText(testFile, largeContent);

        // When
        var hash = _hashService.ComputeFileHash(testFile);

        // Then
        hash.ShouldNotBeNullOrEmpty();
        hash.Length.ShouldBe(64);
    }

    [Fact]
    public void ComputeFileHash_Should_Be_Case_Sensitive_To_Content()
    {
        // Given
        var testFile1 = Path.Combine(_tempDirectory, "test1.md");
        var testFile2 = Path.Combine(_tempDirectory, "test2.md");
        File.WriteAllText(testFile1, "Hello World");
        File.WriteAllText(testFile2, "hello world");

        // When
        var hash1 = _hashService.ComputeFileHash(testFile1);
        var hash2 = _hashService.ComputeFileHash(testFile2);

        // Then
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeFileHash_Should_Detect_Whitespace_Changes()
    {
        // Given
        var testFile1 = Path.Combine(_tempDirectory, "test1.md");
        var testFile2 = Path.Combine(_tempDirectory, "test2.md");
        File.WriteAllText(testFile1, "Hello World");
        File.WriteAllText(testFile2, "Hello  World"); // Extra space

        // When
        var hash1 = _hashService.ComputeFileHash(testFile1);
        var hash2 = _hashService.ComputeFileHash(testFile2);

        // Then
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputeFileHash_Should_Throw_FileNotFoundException_For_Nonexistent_File()
    {
        // Given
        var nonexistentFile = Path.Combine(_tempDirectory, "nonexistent.md");

        // When / Then
        Should.Throw<FileNotFoundException>(() => _hashService.ComputeFileHash(nonexistentFile));
    }

    [Fact]
    public void ComputeFileHash_Should_Handle_Files_With_Special_Characters()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "test.md");
        var content = "Special chars: !@#$%^&*(){}[]|\\:;\"'<>,.?/~`\n\t\r";
        File.WriteAllText(testFile, content);

        // When
        var hash = _hashService.ComputeFileHash(testFile);

        // Then
        hash.ShouldNotBeNullOrEmpty();
        hash.Length.ShouldBe(64);
    }

    [Fact]
    public void ComputeFileHash_Should_Handle_Unicode_Content()
    {
        // Given
        var testFile = Path.Combine(_tempDirectory, "unicode.md");
        var content = "Unicode: 你好世界 🌍 émojis ñoño";
        File.WriteAllText(testFile, content);

        // When
        var hash = _hashService.ComputeFileHash(testFile);

        // Then
        hash.ShouldNotBeNullOrEmpty();
        hash.Length.ShouldBe(64);
    }
}
