namespace markdown_journal_cli.Exceptions;

/// <summary>
/// The base exception class for all journal-related errors in the application.
/// </summary>
public class JournalException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JournalException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public JournalException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public JournalException(string message, Exception inner)
        : base(message, inner) { }
}

/// <summary>
/// Exception thrown when attempting to create a journal that already exists at the specified location.
/// </summary>
public class JournalAlreadyExistsException : JournalException
{
    /// <summary>
    /// Gets the name of the journal that already exists.
    /// </summary>
    public string JournalName { get; }

    /// <summary>
    /// Gets the path where the journal already exists.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalAlreadyExistsException"/> class.
    /// </summary>
    /// <param name="journalName">The name of the journal that already exists.</param>
    /// <param name="path">The path where the journal already exists.</param>
    public JournalAlreadyExistsException(string journalName, string path)
        : base($"Journal '{journalName}' already exists at '{path}'")
    {
        JournalName = journalName;
        Path = path;
    }
}
