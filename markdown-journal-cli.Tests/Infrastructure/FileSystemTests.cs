using markdown_journal_cli.Infrastructure.FileSystem;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="FileSystem"/> class, covering file and directory operations.
/// Note: These tests interact with the real file system but use temporary directories.
/// </summary>
public class FileSystemTests : IDisposable
{
    private readonly FileSystem _fileSystem;
    private readonly string _tempDirectory;

    public FileSystemTests()
    {
        _fileSystem = new FileSystem();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"FileSystemTests_{Guid.NewGuid()}");
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
    public void DirectoryExists_Should_Return_True_For_Existing_Directory()
    {
        // Given
        var testPath = Path.Combine(_tempDirectory, "existing");
        Directory.CreateDirectory(testPath);

        // When
        var result = _fileSystem.DirectoryExists(testPath);

        // Then
        result.ShouldBeTrue();
    }

    [Fact]
    public void DirectoryExists_Should_Return_False_For_Non_Existing_Directory()
    {
        // Given
        var testPath = Path.Combine(_tempDirectory, "nonexistent");

        // When
        var result = _fileSystem.DirectoryExists(testPath);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_Should_Return_True_For_Existing_File()
    {
        // Given
        var fileName = "testfile.txt";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, "test content");

        // When
        var result = _fileSystem.FileExists(filePath);

        // Then
        result.ShouldBeTrue();
    }

    [Fact]
    public void FileExists_Should_Return_False_For_Non_Existing_File()
    {
        // Given
        var filePath = Path.Combine(_tempDirectory, "nonexistent.txt");

        // When
        var result = _fileSystem.FileExists(filePath);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public void FileExists_Should_Return_False_For_Directory_Path()
    {
        // Given
        var dirPath = Path.Combine(_tempDirectory, "testdir");
        Directory.CreateDirectory(dirPath);

        // When
        var result = _fileSystem.FileExists(dirPath);

        // Then
        result.ShouldBeFalse();
    }

    [Fact]
    public void CreateDirectory_Should_Create_New_Directory()
    {
        // Given
        var testPath = Path.Combine(_tempDirectory, "newdir");

        // When
        _fileSystem.CreateDirectory(testPath);

        // Then
        Directory.Exists(testPath).ShouldBeTrue();
    }

    [Fact]
    public void CreateDirectory_Should_Create_Nested_Directories()
    {
        // Given
        var testPath = Path.Combine(_tempDirectory, "nested", "deep", "directory");

        // When
        _fileSystem.CreateDirectory(testPath);

        // Then
        Directory.Exists(testPath).ShouldBeTrue();
    }

    [Fact]
    public void CreateDirectory_Should_Not_Fail_If_Directory_Already_Exists()
    {
        // Given
        var testPath = Path.Combine(_tempDirectory, "existing");
        Directory.CreateDirectory(testPath);

        // When & Then
        Should.NotThrow(() => _fileSystem.CreateDirectory(testPath));
        Directory.Exists(testPath).ShouldBeTrue();
    }

    [Fact]
    public void CombinePaths_Should_Combine_Multiple_Path_Segments()
    {
        // When
        var result = _fileSystem.CombinePaths("root", "sub", "file.txt");

        // Then
        result.ShouldBe(Path.Combine("root", "sub", "file.txt"));
    }

    [Fact]
    public void CombinePaths_Should_Handle_Single_Path()
    {
        // When
        var result = _fileSystem.CombinePaths("singlepath");

        // Then
        result.ShouldBe("singlepath");
    }

    [Fact]
    public void CombinePaths_Should_Handle_Empty_Array()
    {
        // When
        var result = _fileSystem.CombinePaths();

        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void CreateMarkdownFile_Should_Create_File_With_Md_Extension()
    {
        // Given
        var fileName = "testfile";
        var content = "# Test Content";

        // When
        _fileSystem.CreateMarkdownFile(_tempDirectory, fileName, content);

        // Then
        var expectedPath = Path.Combine(_tempDirectory, "testfile.md");
        File.Exists(expectedPath).ShouldBeTrue();
        var actualContent = File.ReadAllText(expectedPath);
        actualContent.ShouldBe(content);
    }

    [Fact]
    public void CreateMarkdownFile_Should_Not_Double_Add_Md_Extension()
    {
        // Given
        var fileName = "testfile.md";
        var content = "# Test Content";

        // When
        _fileSystem.CreateMarkdownFile(_tempDirectory, fileName, content);

        // Then
        var expectedPath = Path.Combine(_tempDirectory, "testfile.md");
        File.Exists(expectedPath).ShouldBeTrue();
        File.Exists(Path.Combine(_tempDirectory, "testfile.md.md")).ShouldBeFalse();
    }

    [Fact]
    public void CreateMarkdownFile_Should_Create_File_When_Directory_Exists()
    {
        // Given
        var subPath = Path.Combine(_tempDirectory, "subdir");
        Directory.CreateDirectory(subPath);
        var fileName = "testfile";
        var content = "# Test Content";

        // When
        _fileSystem.CreateMarkdownFile(subPath, fileName, content);

        // Then
        var expectedFilePath = Path.Combine(subPath, "testfile.md");
        File.Exists(expectedFilePath).ShouldBeTrue();
    }

    [Fact]
    public void CreateMarkdownFile_Should_Throw_When_Directory_Does_Not_Exist()
    {
        // Given
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");
        var fileName = "testfile";
        var content = "# Test Content";

        // When & Then
        Should.Throw<DirectoryNotFoundException>(() =>
            _fileSystem.CreateMarkdownFile(nonExistentPath, fileName, content));
    }

    [Fact]
    public void CreateMarkdownFile_Should_Not_Overwrite_Existing_File()
    {
        // Given
        var fileName = "testfile";
        var originalContent = "# Original Content";
        var newContent = "# New Content";
        var filePath = Path.Combine(_tempDirectory, "testfile.md");

        File.WriteAllText(filePath, originalContent);

        // When & Then
        Should.Throw<InvalidOperationException>(() =>
        _fileSystem.CreateMarkdownFile(_tempDirectory, fileName, newContent));
    
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void CreateMarkdownFile_Should_Handle_Empty_And_Whitespace_Content(string content)
    {
        // Given
        var fileName = "testfile";

        // When
        _fileSystem.CreateMarkdownFile(_tempDirectory, fileName, content);

        // Then
        var expectedPath = Path.Combine(_tempDirectory, "testfile.md");
        File.Exists(expectedPath).ShouldBeTrue();
        var actualContent = File.ReadAllText(expectedPath);
        actualContent.ShouldBe(content);
    }

    /// 
    [Fact]
    public void CreateFile_Should_Create_File()
    {
        // Given
        var fileName = "testfile.txt";
        var content = "# Test Content";

        // When
        _fileSystem.CreateFile(_tempDirectory, fileName, content);

        // Then
        var expectedPath = Path.Combine(_tempDirectory, "testfile.txt");
        File.Exists(expectedPath).ShouldBeTrue();
        var actualContent = File.ReadAllText(expectedPath);
        actualContent.ShouldBe(content);
    }

    [Fact]
    public void CreateFile_Should_Create_File_When_Directory_Exists()
    {
        // Given
        var subPath = Path.Combine(_tempDirectory, "subdir");
        Directory.CreateDirectory(subPath);
        var fileName = ".testfile";
        var content = "# Test Content";

        // When
        _fileSystem.CreateFile(subPath, fileName, content);

        // Then
        var expectedFilePath = Path.Combine(subPath, ".testfile");
        File.Exists(expectedFilePath).ShouldBeTrue();
    }

    [Fact]
    public void CreateFile_Should_Throw_When_Directory_Does_Not_Exist()
    {
        // Given
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");
        var fileName = ".testfile";
        var content = "# Test Content";

        // When & Then
        Should.Throw<DirectoryNotFoundException>(() => 
            _fileSystem.CreateFile(nonExistentPath, fileName, content));
    }

    [Fact]
    public void CreateFile_Should_Overwrite_Existing_File()
    {
        // Given
        var fileName = ".testfile";
        var originalContent = "# Original Content";
        var newContent = "# New Content";
        var filePath = Path.Combine(_tempDirectory, ".testfile");
        
        File.WriteAllText(filePath, originalContent);

        // When % Then
         Should.Throw<InvalidOperationException>(() =>
        _fileSystem.CreateFile(_tempDirectory, fileName, newContent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void CreateFile_Should_Handle_Empty_And_Whitespace_Content(string content)
    {
        // Given
        var fileName = ".testfile";

        // When
        _fileSystem.CreateFile(_tempDirectory, fileName, content);

        // Then
        var expectedPath = Path.Combine(_tempDirectory, ".testfile");
        File.Exists(expectedPath).ShouldBeTrue();
        var actualContent = File.ReadAllText(expectedPath);
        actualContent.ShouldBe(content);
    }
}
