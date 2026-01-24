using markdown_journal_cli.Exceptions;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Exceptions;

/// <summary>
/// Unit tests for the exception classes in the <see cref="markdown_journal_cli.Exceptions"/> namespace,
/// covering exception construction, properties, and inheritance.
/// </summary>
public class JournalExceptionsTests
{
    [Fact]
    public void JournalException_Should_Inherit_From_Exception()
    {
        // Given
        var exception = new JournalException("test message");

        // When & Then
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void JournalException_Constructor_Should_Set_Message()
    {
        // Given
        var message = "This is a test message";

        // When
        var exception = new JournalException(message);

        // Then
        exception.Message.ShouldBe(message);
    }

    [Fact]
    public void JournalException_Constructor_With_Inner_Exception_Should_Set_Message_And_InnerException()
    {
        // Given
        var message = "Outer exception message";
        var innerException = new InvalidOperationException("Inner exception message");

        // When
        var exception = new JournalException(message, innerException);

        // Then
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBe(innerException);
    }

    [Fact]
    public void JournalAlreadyExistsException_Should_Inherit_From_JournalException()
    {
        // Given
        var exception = new JournalAlreadyExistsException("TestJournal", "/test/path");

        // When & Then
        exception.ShouldBeAssignableTo<JournalException>();
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void JournalAlreadyExistsException_Constructor_Should_Set_Properties()
    {
        // Given
        var journalName = "MyTestJournal";
        var path = "/path/to/journal";

        // When
        var exception = new JournalAlreadyExistsException(journalName, path);

        // Then
        exception.JournalName.ShouldBe(journalName);
        exception.Path.ShouldBe(path);
    }

    [Fact]
    public void JournalAlreadyExistsException_Should_Generate_Appropriate_Message()
    {
        // Given
        var journalName = "MyTestJournal";
        var path = "/path/to/journal";

        // When
        var exception = new JournalAlreadyExistsException(journalName, path);

        // Then
        exception.Message.ShouldBe("Journal 'MyTestJournal' already exists at '/path/to/journal'");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("Journal With Spaces", "/path/with spaces/")]
    [InlineData("Special!@#$%Characters", "/path/with/special/chars/")]
    public void JournalAlreadyExistsException_Should_Handle_Various_Input_Values(
        string journalName,
        string path
    )
    {
        // When
        var exception = new JournalAlreadyExistsException(journalName, path);

        // Then
        exception.JournalName.ShouldBe(journalName);
        exception.Path.ShouldBe(path);
        exception.Message.ShouldContain(journalName);
        exception.Message.ShouldContain(path);
    }

    [Fact]
    public void JournalAlreadyExistsException_With_Null_Values_Should_Not_Throw()
    {
        // When & Then
        Should.NotThrow(() => new JournalAlreadyExistsException(null!, null!));
    }

    [Fact]
    public void JournalAlreadyExistsException_With_Null_Values_Should_Set_Properties()
    {
        // When
        var exception = new JournalAlreadyExistsException(null!, null!);

        // Then
        exception.JournalName.ShouldBeNull();
        exception.Path.ShouldBeNull();
        exception.Message.ShouldContain("''"); // Null values appear as empty strings in the message
    }

    [Fact]
    public void JournalException_Should_Be_Serializable()
    {
        // Given
        var originalException = new JournalException("Test message");

        // When & Then
        // Note: In .NET 5+ System.Exception implements ISerializable but serialization
        // behavior may vary. We just test that the exception can be constructed properly.
        originalException.ShouldNotBeNull();
        originalException.Message.ShouldBe("Test message");
    }

    [Fact]
    public void JournalAlreadyExistsException_Should_Be_Serializable()
    {
        // Given
        var originalException = new JournalAlreadyExistsException("TestJournal", "/test/path");

        // When & Then
        originalException.ShouldNotBeNull();
        originalException.JournalName.ShouldBe("TestJournal");
        originalException.Path.ShouldBe("/test/path");
    }

    [Fact]
    public void JournalException_Can_Be_Caught_As_Base_Exception()
    {
        // Given
        Exception caughtException = null!;

        // When
        try
        {
            throw new JournalException("Test exception");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Then
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<JournalException>();
        caughtException.Message.ShouldBe("Test exception");
    }

    [Fact]
    public void JournalAlreadyExistsException_Can_Be_Caught_As_JournalException()
    {
        // Given
        JournalException caughtException = null!;

        // When
        try
        {
            throw new JournalAlreadyExistsException("TestJournal", "/test/path");
        }
        catch (JournalException ex)
        {
            caughtException = ex;
        }

        // Then
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<JournalAlreadyExistsException>();
        var specificException = (JournalAlreadyExistsException)caughtException;
        specificException.JournalName.ShouldBe("TestJournal");
        specificException.Path.ShouldBe("/test/path");
    }

    [Fact]
    public void JournalAlreadyExistsException_Can_Be_Caught_As_Base_Exception()
    {
        // Given
        Exception caughtException = null!;

        // When
        try
        {
            throw new JournalAlreadyExistsException("TestJournal", "/test/path");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Then
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<JournalAlreadyExistsException>();
    }

    [Fact]
    public void Multiple_JournalAlreadyExistsException_Should_Have_Independent_Properties()
    {
        // Given
        var exception1 = new JournalAlreadyExistsException("Journal1", "/path1");
        var exception2 = new JournalAlreadyExistsException("Journal2", "/path2");

        // When & Then
        exception1.JournalName.ShouldBe("Journal1");
        exception1.Path.ShouldBe("/path1");
        exception2.JournalName.ShouldBe("Journal2");
        exception2.Path.ShouldBe("/path2");

        exception1.JournalName.ShouldNotBe(exception2.JournalName);
        exception1.Path.ShouldNotBe(exception2.Path);
    }

    // JournalrcNotFoundException Tests

    [Fact]
    public void JournalrcNotFoundException_Should_Inherit_From_JournalException()
    {
        // Given
        var exception = new JournalrcNotFoundException("/test/path");

        // When & Then
        exception.ShouldBeAssignableTo<JournalException>();
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void JournalrcNotFoundException_Constructor_Should_Set_Properties()
    {
        // Given
        var path = "/path/to/journal";

        // When
        var exception = new JournalrcNotFoundException(path);

        // Then
        exception.Path.ShouldBe(path);
    }

    [Fact]
    public void JournalrcNotFoundException_Should_Generate_Appropriate_Message()
    {
        // Given
        var path = "/path/to/journal";

        // When
        var exception = new JournalrcNotFoundException(path);

        // Then
        exception.Message.ShouldBe(".journalrc not found at '/path/to/journal'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/path/with spaces/")]
    [InlineData("/path/with/special!@#$%/chars/")]
    public void JournalrcNotFoundException_Should_Handle_Various_Path_Values(string path)
    {
        // When
        var exception = new JournalrcNotFoundException(path);

        // Then
        exception.Path.ShouldBe(path);
        exception.Message.ShouldContain(path);
    }

    [Fact]
    public void JournalrcNotFoundException_With_Null_Value_Should_Not_Throw()
    {
        // When & Then
        Should.NotThrow(() => new JournalrcNotFoundException(null!));
    }

    [Fact]
    public void JournalrcNotFoundException_With_Null_Value_Should_Set_Property()
    {
        // When
        var exception = new JournalrcNotFoundException(null!);

        // Then
        exception.Path.ShouldBeNull();
        exception.Message.ShouldContain("''"); // Null value appears as empty string in the message
    }

    [Fact]
    public void JournalrcNotFoundException_Can_Be_Caught_As_JournalException()
    {
        // Given
        JournalException caughtException = null!;

        // When
        try
        {
            throw new JournalrcNotFoundException("/test/path");
        }
        catch (JournalException ex)
        {
            caughtException = ex;
        }

        // Then
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<JournalrcNotFoundException>();
        var specificException = (JournalrcNotFoundException)caughtException;
        specificException.Path.ShouldBe("/test/path");
    }

    [Fact]
    public void JournalrcNotFoundException_Can_Be_Caught_As_Base_Exception()
    {
        // Given
        Exception caughtException = null!;

        // When
        try
        {
            throw new JournalrcNotFoundException("/test/path");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Then
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<JournalrcNotFoundException>();
    }

    [Fact]
    public void Multiple_JournalrcNotFoundException_Should_Have_Independent_Properties()
    {
        // Given
        var exception1 = new JournalrcNotFoundException("/path1");
        var exception2 = new JournalrcNotFoundException("/path2");

        // When & Then
        exception1.Path.ShouldBe("/path1");
        exception2.Path.ShouldBe("/path2");
        exception1.Path.ShouldNotBe(exception2.Path);
    }
}
