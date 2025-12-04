using System;
using markdown_journal_cli.Infrastructure.Configuration.Objects;

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
}
