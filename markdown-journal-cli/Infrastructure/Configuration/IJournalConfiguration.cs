using System;
using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Provides operations for creating, updating, ensuring, and deleting
/// a journal configuration stored in a directory.
/// </summary>
public interface IJournalConfiguration
{
    /// <summary>
    /// Creates a journal configuration file in the specified <paramref name="directory"/>
    /// using the provided <paramref name="config"/> instance.
    /// </summary>
    /// <param name="directory">The directory in which to create the configuration file.</param>
    /// <param name="config">The configuration to write to disk.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> or <paramref name="config"/> is <c>null</c> or empty.</exception>
    void Create(string directory, JournalConfig config);

    /// <summary>
    /// Updates an existing journal configuration in the specified <paramref name="directory"/>
    /// by applying the provided <paramref name="config"/> action to the loaded configuration
    /// and persisting the changes.
    /// </summary>
    /// <param name="directory">The directory containing the configuration to update.</param>
    /// <param name="config">An action that modifies the loaded <see cref="JournalConfig"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> or <paramref name="config"/> is <c>null</c> or empty.</exception>
    void Update(string directory, Action<JournalConfig> config);

    /// <summary>
    /// Deletes the journal configuration file from the specified <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The directory from which to remove the configuration file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> is <c>null</c> or empty.</exception>
    void Delete(string directory);

    /// <summary>
    /// Reads and returns the journal configuration from the specified <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The directory containing the configuration to read.</param>
    /// <returns>The deserialized <see cref="JournalConfig"/> or null if not found or invalid.</returns>
    JournalConfig? Read(string directory);

    /// <summary>
    /// Adds a entry to the ignoreFiles list of journal configuration. ignored entries do not
    /// appear in the table of contents (e.g., 1b-Introduction.md).
    /// </summary>
    /// <param name="directory">The directory containing the configuration.</param>
    /// <param name="file">The filename of the root entry.</param>
    void AddIgnoreEntry(string directory, string file);

    /// <summary>
    /// Adds a root entry to the journal configuration. Root entries are top-level files
    /// that appear at the beginning of the table of contents (e.g., 1b-Introduction.md).
    /// </summary>
    /// <param name="directory">The directory containing the configuration.</param>
    /// <param name="name">The display name of the root entry.</param>
    /// <param name="file">The filename of the root entry.</param>
    void AddRootEntry(string directory, string name, string file);

    /// <summary>
    /// Adds a file to the journal configuration's topic structure based on the topic path.
    /// Creates missing topics/subtopics as needed and avoids duplicates.
    /// </summary>
    /// <param name="directory">The directory containing the configuration.</param>
    /// <param name="topicPath">Array of topic names forming the hierarchy (e.g., ["Learning", "Rust Programming"]).</param>
    /// <param name="entryName">The display name of the entry.</param>
    /// <param name="file">The filename to add to the structure.</param>
    /// <param name="maxDepth">Maximum nesting depth allowed. Use null for unlimited depth.</param>
    /// <param name="sortAlphabetically">Whether to sort topics alphabetically (true) or maintain insertion order (false).</param>
    void AddTopicEntry(string directory, string[] topicPath, string entryName, string file, int? maxDepth = null, bool sortAlphabetically = true);

    /// <summary>
    /// Adds an entry to the journal configuration, automatically determining whether it should be
    /// a root entry or topic entry based on the filename pattern (1a-9z for root entries).
    /// If no topic path is provided for non-root entries, parses the filename to extract topic hierarchy.
    /// </summary>
    /// <param name="directory">The directory containing the configuration.</param>
    /// <param name="name">The display name of the entry.</param>
    /// <param name="file">The filename to add.</param>
    /// <param name="topicPath">Array of topic names forming the hierarchy. If null or empty, parses from filename (e.g., "Learning-Rust" becomes ["Learning", "Rust"]).</param>
    /// <param name="maxDepth">Maximum nesting depth allowed for topic entries. Use null for unlimited depth.</param>
    /// <param name="sortAlphabetically">Whether to sort topics alphabetically (true) or maintain insertion order (false).</param>
    void AddEntry(string directory, string name, string file, string[]? topicPath = null, int? maxDepth = null, bool sortAlphabetically = true, bool ignoreFile = false);

    /// <summary>
    /// Updates the display name of an entry identified by its file name.
    /// Searches through root entries and all topics/subtopics recursively.
    /// </summary>
    /// <param name="directory">The directory containing the configuration.</param>
    /// <param name="file">The filename of the entry to update.</param>
    /// <param name="newEntryName">The new display name for the entry.</param>
    /// <returns>True if the entry was found and updated, false otherwise.</returns>
    bool UpdateEntryName(string directory, string file, string newEntryName);
}
