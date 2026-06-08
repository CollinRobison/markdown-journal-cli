namespace markdown_journal_cli.Services.AddToc;

/// <summary>
/// Describes the outcome of an <c>add toc</c> operation.
/// </summary>
public enum AddTocResult
{
    /// <summary>All requested artifacts were created.</summary>
    Created,

    /// <summary>
    /// Some, but not all, requested artifacts were created because at least one already existed.
    /// Only applicable when both artifacts are requested (no filter flags).
    /// </summary>
    PartiallyCreated,

    /// <summary>All requested artifacts already existed — nothing was written.</summary>
    AlreadyExists,
}

/// <summary>
/// Creates the TOC artifacts for an existing journal.
/// </summary>
public interface IAddTocService
{
    /// <summary>
    /// Creates the requested TOC artifacts inside the journal directory.
    /// </summary>
    /// <param name="journalDir">Absolute path to the journal root directory.</param>
    /// <param name="structureOnly">When <c>true</c>, only <c>.journaltoc</c> is created.</param>
    /// <param name="mdOnly">When <c>true</c>, only the markdown TOC file is created.</param>
    /// <param name="tocName">Optional override for the markdown TOC filename (without <c>.md</c>). When <c>null</c>, the name from <c>.journalrc</c> is used.</param>
    /// <returns>
    /// <see cref="AddTocResult.Created"/> — all requested artifacts were created.
    /// <see cref="AddTocResult.PartiallyCreated"/> — one artifact already existed; the other was created.
    /// <see cref="AddTocResult.AlreadyExists"/> — all requested artifacts already existed.
    /// </returns>
    AddTocResult Execute(
        string journalDir,
        bool structureOnly = false,
        bool mdOnly = false,
        string? tocName = null
    );
}
