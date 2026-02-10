using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
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
        _fileSystem = new FileSystem(NullLogger<FileSystem>.Instance);
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
            _fileSystem.CreateMarkdownFile(nonExistentPath, fileName, content)
        );
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
            _fileSystem.CreateMarkdownFile(_tempDirectory, fileName, newContent)
        );
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
            _fileSystem.CreateFile(nonExistentPath, fileName, content)
        );
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
            _fileSystem.CreateFile(_tempDirectory, fileName, newContent)
        );
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

    #region GetFileContent Tests

    [Fact]
    public void GetFileContent_Should_Read_File_Content_Successfully()
    {
        // Given
        var fileName = "testfile.txt";
        var expectedContent = "This is test content\nwith multiple lines\nand special chars: !@#$%";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Fact]
    public void GetFileContent_Should_Read_Empty_File()
    {
        // Given
        var fileName = "emptyfile.txt";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, string.Empty);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBeEmpty();
    }

    [Fact]
    public void GetFileContent_Should_Read_Markdown_File()
    {
        // Given
        var fileName = "testfile.md";
        var expectedContent = "# Header\n\n## Subheader\n\n- Item 1\n- Item 2\n\n**Bold text**";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Fact]
    public void GetFileContent_Should_Read_JSON_File()
    {
        // Given
        var fileName = "config.json";
        var expectedContent = "{\"key\": \"value\", \"number\": 42}";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Fact]
    public void GetFileContent_Should_Read_File_With_Unicode_Characters()
    {
        // Given
        var fileName = "unicode.txt";
        var expectedContent = "Hello 世界 🌍 café naïve résumé";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Fact]
    public void GetFileContent_Should_Read_File_With_Windows_Line_Endings()
    {
        // Given
        var fileName = "windows.txt";
        var expectedContent = "Line 1\r\nLine 2\r\nLine 3";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Fact]
    public void GetFileContent_Should_Throw_FileNotFoundException_When_File_Does_Not_Exist()
    {
        // Given
        var filePath = Path.Combine(_tempDirectory, "nonexistent.txt");

        // When & Then
        var exception = Should.Throw<FileNotFoundException>(() =>
            _fileSystem.GetFileContent(filePath)
        );
        exception.Message.ShouldContain(filePath);
    }

    [Fact]
    public void GetFileContent_Should_Throw_When_Path_Is_Directory()
    {
        // Given
        var dirPath = Path.Combine(_tempDirectory, "testdir");
        Directory.CreateDirectory(dirPath);

        // When & Then
        Should.Throw<Exception>(() =>
            _fileSystem.GetFileContent(dirPath)
        );
    }

    [Fact]
    public void GetFileContent_Should_Read_File_From_Nested_Directory()
    {
        // Given
        var nestedPath = Path.Combine(_tempDirectory, "level1", "level2", "level3");
        Directory.CreateDirectory(nestedPath);
        var fileName = "nested.txt";
        var expectedContent = "Content in nested directory";
        var filePath = Path.Combine(nestedPath, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    [Theory]
    [InlineData("Single line content")]
    [InlineData("Line 1\nLine 2\nLine 3")]
    [InlineData("   Leading whitespace")]
    [InlineData("Trailing whitespace   ")]
    [InlineData("\tTab\tcharacters\t")]
    public void GetFileContent_Should_Preserve_Content_Exactly(string expectedContent)
    {
        // Given
        var fileName = "preserve.txt";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, expectedContent);

        // When
        var actualContent = _fileSystem.GetFileContent(filePath);

        // Then
        actualContent.ShouldBe(expectedContent);
    }

    #endregion

    #region UpdateFile Tests

    [Fact]
    public void UpdateFile_Should_Update_Existing_File()
    {
        // Given
        var fileName = "update.txt";
        var originalContent = "Original content";
        var newContent = "Updated content";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, originalContent);

        // When
        _fileSystem.UpdateFile(_tempDirectory, fileName, newContent);

        // Then
        var actualContent = File.ReadAllText(filePath);
        actualContent.ShouldBe(newContent);
    }

    [Fact]
    public void UpdateFile_Should_Create_File_If_Does_Not_Exist()
    {
        // Given
        var fileName = "newfile.txt";
        var content = "New content";
        var filePath = Path.Combine(_tempDirectory, fileName);

        // When
        _fileSystem.UpdateFile(_tempDirectory, fileName, content);

        // Then
        File.Exists(filePath).ShouldBeTrue();
        var actualContent = File.ReadAllText(filePath);
        actualContent.ShouldBe(content);
    }

    [Fact]
    public void UpdateFile_Should_Completely_Replace_Content()
    {
        // Given
        var fileName = "replace.txt";
        var originalContent = "Original content\nwith multiple\nlines";
        var newContent = "Short";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, originalContent);

        // When
        _fileSystem.UpdateFile(_tempDirectory, fileName, newContent);

        // Then
        var actualContent = File.ReadAllText(filePath);
        actualContent.ShouldBe(newContent);
        actualContent.Length.ShouldBeLessThan(originalContent.Length);
    }

    [Fact]
    public void UpdateFile_Should_Handle_Empty_Content()
    {
        // Given
        var fileName = "empty.txt";
        var originalContent = "Some content";
        var newContent = string.Empty;
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, originalContent);

        // When
        _fileSystem.UpdateFile(_tempDirectory, fileName, newContent);

        // Then
        var actualContent = File.ReadAllText(filePath);
        actualContent.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateFile_Should_Work_With_Different_File_Extensions()
    {
        // Given
        var fileName = "config.json";
        var content = "{\"updated\": true}";

        // When
        _fileSystem.UpdateFile(_tempDirectory, fileName, content);

        // Then
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.Exists(filePath).ShouldBeTrue();
        var actualContent = File.ReadAllText(filePath);
        actualContent.ShouldBe(content);
    }

    #endregion

    #region DeleteFile Tests

    [Fact]
    public void DeleteFile_Should_Delete_Existing_File()
    {
        // Given
        var fileName = "deleteme.txt";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, "content");

        // When
        _fileSystem.DeleteFile(filePath);

        // Then
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void DeleteFile_Should_Not_Throw_When_File_Does_Not_Exist()
    {
        // Given
        var filePath = Path.Combine(_tempDirectory, "nonexistent.txt");

        // When & Then
        Should.NotThrow(() => _fileSystem.DeleteFile(filePath));
    }

    [Fact]
    public void DeleteFile_Should_Delete_Markdown_File()
    {
        // Given
        var fileName = "deleteme.md";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, "# Content");

        // When
        _fileSystem.DeleteFile(filePath);

        // Then
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void DeleteFile_Should_Delete_Hidden_File()
    {
        // Given
        var fileName = ".hiddenfile";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, "content");

        // When
        _fileSystem.DeleteFile(filePath);

        // Then
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void DeleteFile_Should_Delete_File_From_Nested_Directory()
    {
        // Given
        var nestedPath = Path.Combine(_tempDirectory, "nested", "deep");
        Directory.CreateDirectory(nestedPath);
        var fileName = "nested.txt";
        var filePath = Path.Combine(nestedPath, fileName);
        File.WriteAllText(filePath, "content");

        // When
        _fileSystem.DeleteFile(filePath);

        // Then
        File.Exists(filePath).ShouldBeFalse();
        Directory.Exists(nestedPath).ShouldBeTrue(); // Directory should still exist
    }

    #endregion
}
